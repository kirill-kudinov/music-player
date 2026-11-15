using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Playlists;
using MusicPlayer.Core.Primitives;
using MusicPlayer.Features.Importer;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Playlists.Data;
using MusicPlayer.Features.Tracks;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Playlists;

public sealed partial class PlaylistDetailViewModel : ObservableObject, IQueryAttributable, IDisposable
{
    private static readonly ImageSource FavoritesCoverImage = ImageSource.FromFile("playlist_favorites_cover.png");
    private static readonly ImageSource PlaceholderCoverImage = ImageSource.FromFile("playlist_cover_placeholder.png");

    private readonly IPlaylistRepository _repository;
    private readonly TrackStorage _trackStorage;
    private readonly IMediaStoreAudioScanner? _mediaStoreScanner;
    private readonly ITrackCoverExtractor _coverExtractor;
    private readonly IPlaybackQueue _playbackQueue;
    private readonly IAudioPlayer _audioPlayer;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private Guid _playlistId;
    private Playlist? _currentPlaylist;
    private int _reloadRequested;
    private int _suppressReloadCount;
    private bool _isDisposed;

    public PlayerViewModel Player { get; }

    public ObservableCollection<TrackItemViewModel> Tracks { get; } = [];

    public ObservableCollection<SelectableTrackItemViewModel> AvailableTracksToAdd { get; } = [];

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _trackCountText = "0 треков";

    [ObservableProperty]
    private ImageSource _coverSource = PlaceholderCoverImage;

    [ObservableProperty]
    private bool _hasTracks;

    [ObservableProperty]
    private TrackItemViewModel? _selectedTrackForOptions;

    [ObservableProperty]
    private bool _isTrackOptionsPopupVisible;

    [ObservableProperty]
    private bool _isAddTracksSheetVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAvailableTracksToAdd))]
    private bool _hasAvailableTracksToAdd;

    public bool HasNoAvailableTracksToAdd => !HasAvailableTracksToAdd;

    [ObservableProperty]
    private bool _canConfirmAddTracks;

    public PlaylistDetailViewModel(
        IPlaylistRepository repository,
        TrackStorage trackStorage,
        ITrackCoverExtractor coverExtractor,
        IPlaybackQueue playbackQueue,
        IAudioPlayer audioPlayer,
        PlayerViewModel playerViewModel,
        IMediaStoreAudioScanner? mediaStoreScanner = null)
    {
        _repository = repository;
        _trackStorage = trackStorage;
        _mediaStoreScanner = mediaStoreScanner;
        _coverExtractor = coverExtractor;
        _playbackQueue = playbackQueue;
        _audioPlayer = audioPlayer;
        Player = playerViewModel;

        _repository.PlaylistsChanged += OnRepositoryPlaylistsChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("playlistId", out object? rawId) &&
            rawId is string idString &&
            Guid.TryParse(idString, out Guid parsedId))
        {
            _playlistId = parsedId;
            _ = LoadPlaylistAsync();
        }
    }

    public void RefreshIfLoaded()
    {
        Player.Tick();
        if (_playlistId != Guid.Empty)
        {
            _ = LoadPlaylistAsync();
        }
    }

    public async Task LoadPlaylistAsync()
    {
        if (_playlistId == Guid.Empty)
            return;

        Interlocked.Exchange(ref _reloadRequested, 1);
        if (!await _loadGate.WaitAsync(0))
            return;

        try
        {
            while (Interlocked.Exchange(ref _reloadRequested, 0) == 1)
            {
                Result<Playlist> result = await _repository.GetPlaylistByIdAsync(_playlistId);
                if (!result.TryGetOk(out Playlist? playlist))
                {
                    await ShowToastSafeAsync(result.Error.Message);
                    continue;
                }

                List<TrackItemViewModel> trackItems = await Task.Run(() =>
                {
                    List<TrackItemViewModel> items = new(playlist.Tracks.Count);
                    foreach (AudioTrack track in playlist.Tracks)
                    {
                        ImageSource? cover = _coverExtractor.ExtractCover(track.ContentUri);
                        items.Add(new TrackItemViewModel(track, cover));
                    }

                    return items;
                });

                RunOnMainThread(() =>
                {
                    _currentPlaylist = playlist;
                    Title = playlist.Title;
                    CoverSource = ResolveCoverSource(playlist);

                    Tracks.Clear();
                    foreach (TrackItemViewModel item in trackItems)
                    {
                        Tracks.Add(item);
                    }

                    UpdateTrackSummary();
                });
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }

    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand(CanExecute = nameof(HasTracks))]
    private async Task PlayAllAsync()
    {
        if (Tracks.Count == 0)
            return;

        await PlayFromTrackAsync(Tracks[0]);
    }

    [RelayCommand]
    private async Task PlayFromTrackAsync(TrackItemViewModel? item)
    {
        if (item is null || Tracks.Count == 0)
            return;

        IsTrackOptionsPopupVisible = false;

        List<AudioTrack> queueTracks = Tracks.Select(x => x.Track).ToList();
        Player.PlayContextTitle = string.IsNullOrWhiteSpace(Title) ? "Сейчас играет" : Title;
        _playbackQueue.SetTracks(queueTracks, item.Track);
        _audioPlayer.Play(item.Track.ContentUri);
        Player.NotifyTrackStarted();

        await Player.ExpandCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task OpenAddTracksPickerAsync()
    {
        HashSet<string> existingUris = new(
            Tracks.Select(t => t.Track.ContentUri.OriginalString),
            StringComparer.OrdinalIgnoreCase);

        List<SelectableTrackItemViewModel> candidates = await Task.Run(() =>
        {

            List<AudioTrack> allTracks = _trackStorage.GetAllTracks();
            if (_mediaStoreScanner is not null)
            {
                var seen = new HashSet<string>(allTracks.Select(t => t.ContentUri.OriginalString), StringComparer.OrdinalIgnoreCase);
                foreach (AudioTrack msTrack in _mediaStoreScanner.CachedTracks)
                {
                    if (!_trackStorage.IsExcludedUri(msTrack.ContentUri) && seen.Add(msTrack.ContentUri.OriginalString))
                    {
                        allTracks.Add(msTrack);
                    }
                }
            }
            List<SelectableTrackItemViewModel> result = [];

            foreach (AudioTrack track in allTracks)
            {
                bool isAlreadyInPlaylist = existingUris.Contains(track.ContentUri.OriginalString);
                ImageSource? cover = _coverExtractor.ExtractCover(track.ContentUri);
                result.Add(new SelectableTrackItemViewModel(track, cover, isAlreadyInPlaylist));
            }

            return result;
        });

        AvailableTracksToAdd.Clear();
        foreach (SelectableTrackItemViewModel candidate in candidates)
        {
            AvailableTracksToAdd.Add(candidate);
        }

        HasAvailableTracksToAdd = AvailableTracksToAdd.Count > 0;
        CanConfirmAddTracks = false;
        IsAddTracksSheetVisible = true;
    }

    [RelayCommand]
    private void ToggleTrackSelection(SelectableTrackItemViewModel? item)
    {
        if (item is null || item.IsAlreadyInPlaylist)
            return;

        item.IsSelected = !item.IsSelected;
        CanConfirmAddTracks = AvailableTracksToAdd.Any(x => !x.IsAlreadyInPlaylist && x.IsSelected);
    }

    [RelayCommand]
    private void CloseAddTracksSheet()
    {
        IsAddTracksSheetVisible = false;
    }

    [RelayCommand]
    private async Task ConfirmAddSelectedTracksAsync()
    {
        List<AudioTrack> selectedTracks = AvailableTracksToAdd
            .Where(x => !x.IsAlreadyInPlaylist && x.IsSelected)
            .Select(x => x.Track)
            .ToList();

        IsAddTracksSheetVisible = false;

        if (selectedTracks.Count == 0 || _playlistId == Guid.Empty)
            return;

        Interlocked.Increment(ref _suppressReloadCount);
        try
        {
            Result<int> addResult = await _repository.AddTracksToPlaylistAsync(_playlistId, selectedTracks);
            if (!addResult.TryGetOk(out int addedCount))
            {
                await ShowToastSafeAsync(addResult.Error.Message);
                return;
            }

            await LoadPlaylistAsync();

            if (addedCount > 0)
            {
                await ShowToastSafeAsync($"Добавлено треков: {addedCount}");
            }
        }
        finally
        {
            Interlocked.Decrement(ref _suppressReloadCount);
        }
    }

    [RelayCommand]
    private void OpenTrackOptions(TrackItemViewModel? item)
    {
        if (item is null)
            return;

        SelectedTrackForOptions = item;
        IsTrackOptionsPopupVisible = true;
    }

    [RelayCommand]
    private void CloseTrackOptions()
    {
        IsTrackOptionsPopupVisible = false;
    }

    [RelayCommand]
    private async Task AddTrackToQueueAsync()
    {
        TrackItemViewModel? target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;

        if (target is null)
            return;

        _playbackQueue.Enqueue(target.Track);
        Player.SyncQueueState();

        await ShowToastSafeAsync($"Трек «{target.Track.Title}» добавлен в очередь");
    }

    [RelayCommand]
    private async Task RemoveTrackFromPlaylistAsync()
    {
        TrackItemViewModel? target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;
        SelectedTrackForOptions = null;

        if (target is null || _playlistId == Guid.Empty)
            return;

        Interlocked.Increment(ref _suppressReloadCount);
        try
        {
            Result<Unit> removeResult = await _repository.RemoveTrackFromPlaylistAsync(
                _playlistId,
                target.Track.ContentUri);

            if (removeResult.IsError)
            {
                await ShowToastSafeAsync(removeResult.Error.Message);
                return;
            }

            if (_currentPlaylist?.IsSystem == true)
            {
                target.Track.SetFavorite(false);
            }

            _currentPlaylist?.RemoveTrack(target.Track.ContentUri);
            Tracks.Remove(target);
            UpdateTrackSummary();

            await ShowToastSafeAsync("Трек убран из плейлиста");
        }
        finally
        {
            Interlocked.Decrement(ref _suppressReloadCount);
        }
    }

    private void OnRepositoryPlaylistsChanged(object? sender, EventArgs e)
    {
        if (Volatile.Read(ref _suppressReloadCount) > 0 || _playlistId == Guid.Empty)
            return;

        _ = LoadPlaylistAsync();
    }

    private void UpdateTrackSummary()
    {
        TrackCountText = PlaylistFormatting.FormatTrackCount(Tracks.Count);
        HasTracks = Tracks.Count > 0;
        PlayAllCommand.NotifyCanExecuteChanged();
    }

    private static ImageSource ResolveCoverSource(Playlist playlist)
    {
        if (playlist.IsSystem)
            return FavoritesCoverImage;

        if (playlist.CoverPath.TryGetSome(out string? coverPath) && File.Exists(coverPath))
            return ImageSource.FromFile(coverPath);

        return PlaceholderCoverImage;
    }

    private static void RunOnMainThread(Action action)
    {
        if (MainThread.IsMainThread)
        {
            action();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(action);
        }
    }

    private static async Task ShowToastSafeAsync(string message)
    {
        try
        {
            var toast = Toast.Make(message, ToastDuration.Short, 14d);
            await toast.Show(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaylistDetailViewModel] Toast notification skipped: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _repository.PlaylistsChanged -= OnRepositoryPlaylistsChanged;
        _loadGate.Dispose();
    }
}

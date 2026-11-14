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
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Playlists;
using MusicPlayer.Core.Primitives;
using MusicPlayer.Features.Importer;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Playlists.Data;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Tracks;

public sealed partial class TracksViewModel : ObservableObject, IDisposable
{
    private readonly TrackStorageSettings _storageSettings;
    private readonly TrackStorage _trackStorage;
    private readonly ITrackCoverExtractor _coverExtractor;
    private readonly IPlaybackQueue _playbackQueue;
    private readonly IAudioPlayer _audioPlayer;
    private readonly IPlaylistRepository _playlistRepository;

    private readonly List<TrackItemViewModel> _allTracks = [];
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _isDisposed;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private TrackItemViewModel? _selectedTrackForOptions;

    [ObservableProperty]
    private bool _isTrackOptionsPopupVisible;

    public PlayerViewModel Player { get; }

    public ObservableCollection<TrackItemViewModel> FilteredTracks { get; } = [];

    public TracksViewModel(
        TrackStorageSettings storageSettings,
        TrackStorage trackStorage,
        ITrackCoverExtractor coverExtractor,
        IPlaybackQueue playbackQueue,
        IAudioPlayer audioPlayer,
        PlayerViewModel playerViewModel,
        IPlaylistRepository playlistRepository)
    {
        _storageSettings = storageSettings;
        _trackStorage = trackStorage;
        _coverExtractor = coverExtractor;
        _playbackQueue = playbackQueue;
        _audioPlayer = audioPlayer;
        Player = playerViewModel;
        _playlistRepository = playlistRepository;

        _trackStorage.TrackAdded += OnStorageTrackAdded;
        _trackStorage.TrackRemoved += OnStorageTrackRemoved;
    }

    partial void OnSearchQueryChanged(string value)
    {
        RunOnMainThread(() => ApplyFilter(value));
    }

    public void LoadTracks()
    {
        _ = LoadTracksAsync();
    }

    public async Task LoadTracksAsync()
    {
        Player.Tick();

        if (!Directory.Exists(_storageSettings.BaseDirectory))
            return;

        if (!await _loadGate.WaitAsync(0))
            return;

        try
        {
            List<TrackItemViewModel> newlyLoaded = await Task.Run(() =>
            {
                List<TrackItemViewModel> items = [];

                foreach (string rawFilePath in Directory.EnumerateFiles(_storageSettings.BaseDirectory))
                {
                    string filePath = Path.GetFullPath(rawFilePath);
                    if (_trackStorage.IsExcluded(filePath))
                        continue;

                    lock (_loadedPaths)
                    {
                        if (_loadedPaths.Contains(filePath))
                            continue;
                    }

                    if (TryCreateTrackItem(filePath, out var trackItem))
                    {
                        items.Add(trackItem);
                    }
                }

                return items;
            });

            if (newlyLoaded.Count > 0 || (FilteredTracks.Count == 0 && _allTracks.Count > 0))
            {
                RunOnMainThread(() =>
                {
                    foreach (var item in newlyLoaded)
                    {
                        string path = Path.GetFullPath(item.Track.ContentUri.LocalPath);
                        lock (_loadedPaths)
                        {
                            if (!_loadedPaths.Add(path))
                                continue;
                        }

                        _allTracks.Add(item);
                    }

                    ApplyFilter(SearchQuery);
                    var snapshot = _allTracks.Select(x => x.Track).ToList();
                    _ = SyncFavoritesAndNotifyAsync(snapshot);
                });
            }
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task SyncFavoritesAndNotifyAsync(List<AudioTrack> tracks)
    {
        await _playlistRepository.SyncFavoritesWithTracksAsync(tracks);
        RunOnMainThread(() => Player.SyncFavoriteState());
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
        var target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;

        if (target is null)
            return;

        _playbackQueue.Enqueue(target.Track);
        Player.SyncQueueState();

        await ShowToastSafeAsync($"Трек «{target.Track.Title}» добавлен в очередь");
    }
    

    [RelayCommand]
    private async Task DeleteTrackFromPlayerAsync()
    {
        var target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;

        if (target is null)
            return;

        SelectedTrackForOptions = null;

        RunOnMainThread(() => RemoveTrackItemFromCollections(target.Track.ContentUri));
        Player.HandleTrackRemoved(target.Track);
        _trackStorage.RemoveTrackFromPlayer(target.Track.ContentUri);
        _ = _playlistRepository.RemoveTrackFromEverywhereAsync(target.Track.ContentUri);

        await ShowToastSafeAsync($"Трек «{target.Track.Title}» удалён");
    }

    [RelayCommand]
    private async Task OpenTrackAsync(TrackItemViewModel? item)
    {
        if (item is null)
            return;

        IsTrackOptionsPopupVisible = false;

        var queueTracks = _allTracks.Select(x => x.Track).ToList();
        _playbackQueue.SetTracks(queueTracks, item.Track);

        _audioPlayer.Play(item.Track.ContentUri);
        Player.NotifyTrackStarted();

        await Player.ExpandCommand.ExecuteAsync(null);
    }

    private async void OnStorageTrackAdded(object? sender, string rawFilePath)
    {
        string filePath = Path.GetFullPath(rawFilePath);

        TrackItemViewModel? newItem = await Task.Run(() =>
            TryCreateTrackItem(filePath, out var item) ? item : null);

        if (newItem is null)
            return;

        RunOnMainThread(() =>
        {
            RemoveTrackItemFromCollections(newItem.Track.ContentUri);
            lock (_loadedPaths)
            {
                _loadedPaths.Add(filePath);
            }

            _allTracks.Add(newItem);

            if (MatchesFilter(newItem, SearchQuery))
            {
                FilteredTracks.Add(newItem);
            }
        });
    }

    private void OnStorageTrackRemoved(object? sender, string rawFilePath)
    {
        string filePath = Path.GetFullPath(rawFilePath);
        RunOnMainThread(() => RemoveTrackByPath(filePath));
    }

    private void RemoveTrackByPath(string normalizedFilePath)
    {
        lock (_loadedPaths)
        {
            _loadedPaths.Remove(normalizedFilePath);
        }

        var matching = _allTracks.FirstOrDefault(x =>
            x.Track.ContentUri.IsFile &&
            string.Equals(Path.GetFullPath(x.Track.ContentUri.LocalPath), normalizedFilePath, StringComparison.OrdinalIgnoreCase));

        if (matching is not null)
        {
            RemoveTrackItemFromCollections(matching.Track.ContentUri);
        }
    }

    private void RemoveTrackItemFromCollections(Uri contentUri)
    {
        if (contentUri.IsFile)
        {
            lock (_loadedPaths)
            {
                _loadedPaths.Remove(Path.GetFullPath(contentUri.LocalPath));
            }
        }

        for (int i = _allTracks.Count - 1; i >= 0; i--)
        {
            if (AreSameTrackUri(_allTracks[i].Track.ContentUri, contentUri))
            {
                _allTracks.RemoveAt(i);
            }
        }

        for (int i = FilteredTracks.Count - 1; i >= 0; i--)
        {
            if (AreSameTrackUri(FilteredTracks[i].Track.ContentUri, contentUri))
            {
                FilteredTracks.RemoveAt(i);
            }
        }
    }

    private bool TryCreateTrackItem(string filePath, out TrackItemViewModel item)
    {
        item = null!;
        try
        {
            var trackResult = AudioTrack.Create(filePath);
            if (!trackResult.TryGetOk(out var track))
                return false;

            var cover = _coverExtractor.ExtractCover(track.ContentUri);
            item = new TrackItemViewModel(track, cover);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TracksViewModel] Failed to create AudioTrack for '{filePath}': {ex.Message}");
            return false;
        }
    }

    private void ApplyFilter(string? query)
    {
        FilteredTracks.Clear();
        foreach (var item in _allTracks)
        {
            if (MatchesFilter(item, query))
            {
                FilteredTracks.Add(item);
            }
        }
    }

    private static bool MatchesFilter(TrackItemViewModel item, string? query)
    {
        string trimmed = query?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
            return true;

        return (item.Track.Title?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
               (item.Track.Artist?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool AreSameTrackUri(Uri left, Uri right)
    {
        if (left == right)
            return true;

        if (left.IsFile && right.IsFile)
        {
            return string.Equals(
                Path.GetFullPath(left.LocalPath),
                Path.GetFullPath(right.LocalPath),
                StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(left.OriginalString, right.OriginalString, StringComparison.OrdinalIgnoreCase);
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
            Debug.WriteLine($"[TracksViewModel] Toast notification skipped: {ex.Message}");
        }
    }
    
    [ObservableProperty]
    private TrackItemViewModel? _selectedTrackForPlaylist;

    [ObservableProperty]
    private bool _isSelectPlaylistSheetVisible;

    public ObservableCollection<SelectablePlaylistItemViewModel> AvailablePlaylistsForTrack { get; } = [];

    
    [RelayCommand]
    private async Task AddTrackToPlaylistAsync()
    {
        var target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;

        if (target is null)
            return;

        Result<IReadOnlyList<Playlist>> result = await _playlistRepository.GetAllPlaylistsAsync();
        if (!result.TryGetOk(out IReadOnlyList<Playlist>? playlists))
        {
            await ShowToastSafeAsync(result.Error.Message);
            return;
        }

        RunOnMainThread(() =>
        {
            AvailablePlaylistsForTrack.Clear();
            foreach (Playlist playlist in playlists)
            {
                bool contains = playlist.ContainsTrack(target.Track.ContentUri);
                AvailablePlaylistsForTrack.Add(new SelectablePlaylistItemViewModel(playlist, contains));
            }

            SelectedTrackForPlaylist = target;
            IsSelectPlaylistSheetVisible = true;
        });
    }

    [RelayCommand]
    private void TogglePlaylistSelection(SelectablePlaylistItemViewModel? item)
    {
        if (item is null)
            return;

        item.IsSelected = !item.IsSelected;
    }

    [RelayCommand]
    private void CloseSelectPlaylistSheet()
    {
        IsSelectPlaylistSheetVisible = false;
        SelectedTrackForPlaylist = null;
    }

    [RelayCommand]
    private async Task ConfirmAddToPlaylistsAsync()
    {
        TrackItemViewModel? target = SelectedTrackForPlaylist;
        List<SelectablePlaylistItemViewModel> items = [.. AvailablePlaylistsForTrack];

        IsSelectPlaylistSheetVisible = false;
        SelectedTrackForPlaylist = null;

        if (target is null)
            return;

        bool anyChanged = false;

        foreach (SelectablePlaylistItemViewModel item in items)
        {
            if (item.IsSelected == item.InitiallyContainsTrack)
                continue;

            Result<Unit> opResult = item.IsSelected
                ? await _playlistRepository.AddTrackToPlaylistAsync(item.Id, target.Track)
                : await _playlistRepository.RemoveTrackFromPlaylistAsync(item.Id, target.Track.ContentUri);

            if (opResult.IsError)
            {
                await ShowToastSafeAsync(opResult.Error.Message);
                continue;
            }

            anyChanged = true;

            if (item.IsSystem)
            {
                if (target.Track.IsFavorite != item.IsSelected)
                {
                    target.Track.ToggleFavorite();
                }

                Player.SyncFavoriteState();
            }
        }

        if (anyChanged)
        {
            await ShowToastSafeAsync("Плейлисты обновлены");
        }
    }
    
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _trackStorage.TrackAdded -= OnStorageTrackAdded;
        _trackStorage.TrackRemoved -= OnStorageTrackRemoved;
        _loadGate.Dispose();
    }
}
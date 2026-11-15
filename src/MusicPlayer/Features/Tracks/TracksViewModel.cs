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
    private readonly IMediaStoreAudioScanner? _mediaStoreScanner;

    private readonly HashSet<TrackItemViewModel> _allTracks = [];
    private readonly Dictionary<string, TrackItemViewModel> _tracksByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _isDisposed;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private TrackItemViewModel? _selectedTrackForOptions;

    [ObservableProperty]
    private bool _isTrackOptionsPopupVisible;

    [ObservableProperty]
    private TrackItemViewModel? _selectedTrackForPlaylist;

    [ObservableProperty]
    private bool _isSelectPlaylistSheetVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoAvailablePlaylistsForTrack))]
    private bool _hasAvailablePlaylistsForTrack;

    public bool HasNoAvailablePlaylistsForTrack => !HasAvailablePlaylistsForTrack;

    public PlayerViewModel Player { get; }

    public ObservableCollection<TrackItemViewModel> FilteredTracks { get; } = [];

    public ObservableCollection<SelectablePlaylistItemViewModel> AvailablePlaylistsForTrack { get; } = [];

    public TracksViewModel(
        TrackStorageSettings storageSettings,
        TrackStorage trackStorage,
        ITrackCoverExtractor coverExtractor,
        IPlaybackQueue playbackQueue,
        IAudioPlayer audioPlayer,
        PlayerViewModel playerViewModel,
        IPlaylistRepository playlistRepository,
        IMediaStoreAudioScanner? mediaStoreScanner = null)
    {
        _storageSettings = storageSettings;
        _trackStorage = trackStorage;
        _coverExtractor = coverExtractor;
        _playbackQueue = playbackQueue;
        _audioPlayer = audioPlayer;
        Player = playerViewModel;
        _playlistRepository = playlistRepository;
        _mediaStoreScanner = mediaStoreScanner;

        _trackStorage.TrackAdded += OnStorageTrackAdded;
        _trackStorage.TrackRemoved += OnStorageTrackRemoved;
        _playlistRepository.TrackFavoriteChanged += OnRepositoryTrackFavoriteChanged;
        if (_mediaStoreScanner is not null)
        {
            _mediaStoreScanner.TracksScanned += OnMediaStoreTracksScanned;
        }
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

        if (!await _loadGate.WaitAsync(0))
            return;

        try
        {
            if (_mediaStoreScanner is not null &&
                _mediaStoreScanner.CachedTracks.Count == 0 &&
                await _mediaStoreScanner.HasReadAudioPermissionAsync())
            {
                await _mediaStoreScanner.ScanDeviceAudioAsync();
            }

            List<TrackItemViewModel> newlyLoaded = await Task.Run(() =>
            {
                List<TrackItemViewModel> items = [];

                if (_mediaStoreScanner is not null)
                {
                    foreach (AudioTrack track in _mediaStoreScanner.CachedTracks)
                    {
                        if (_trackStorage.IsExcludedUri(track.ContentUri))
                            continue;

                        string uriKey = track.ContentUri.OriginalString;
                        lock (_loadedPaths)
                        {
                            if (_loadedPaths.Contains(uriKey))
                                continue;
                        }

                        ImageSource? cover = _coverExtractor.ExtractCover(track.ContentUri);
                        items.Add(new TrackItemViewModel(track, cover));
                    }
                }

                if (Directory.Exists(_storageSettings.BaseDirectory))
                {
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
                }

                return items;
            });

            if (newlyLoaded.Count > 0 || (FilteredTracks.Count == 0 && _allTracks.Count > 0))
            {
                RunOnMainThread(() =>
                {
                    foreach (var item in newlyLoaded)
                    {
                        string path = (item.Track.ContentUri.IsAbsoluteUri && item.Track.ContentUri.IsFile)
                            ? Path.GetFullPath(item.Track.ContentUri.LocalPath)
                            : item.Track.ContentUri.OriginalString;
                        lock (_loadedPaths)
                        {
                            if (!_loadedPaths.Add(path))
                                continue;
                        }

                        _tracksByKey[path] = item;
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
        await _playlistRepository.RemoveTrackFromEverywhereAsync(target.Track.ContentUri);

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

    [RelayCommand]
    private async Task AddTrackToPlaylistAsync()
    {
        var target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;

        if (target is null)
            return;

        Task<Result<IReadOnlyList<Playlist>>> playlistsTask = _playlistRepository.GetAllPlaylistsAsync();
        // Даём нижней шторке опций время плавно уехать, чтобы не накладывать две анимации.
        await Task.WhenAll(playlistsTask, Task.Delay(120));

        Result<IReadOnlyList<Playlist>> result = await playlistsTask;
        if (!result.TryGetOk(out IReadOnlyList<Playlist>? playlists))
        {
            await ShowToastSafeAsync(result.Error.Message);
            return;
        }

        List<SelectablePlaylistItemViewModel> items = await Task.Run(() =>
            playlists.Select(p => new SelectablePlaylistItemViewModel(p, p.ContainsTrack(target.Track.ContentUri))).ToList());

        RunOnMainThread(() =>
        {
            AvailablePlaylistsForTrack.Clear();
            foreach (SelectablePlaylistItemViewModel item in items)
            {
                AvailablePlaylistsForTrack.Add(item);
            }

            HasAvailablePlaylistsForTrack = AvailablePlaylistsForTrack.Count > 0;
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

        List<Guid> playlistIdsToAdd = [];
        List<Guid> playlistIdsToRemove = [];

        foreach (SelectablePlaylistItemViewModel item in items)
        {
            if (item.IsSelected == item.InitiallyContainsTrack)
                continue;

            if (item.IsSelected)
            {
                playlistIdsToAdd.Add(item.Id);
            }
            else
            {
                playlistIdsToRemove.Add(item.Id);
            }
        }

        if (playlistIdsToAdd.Count == 0 && playlistIdsToRemove.Count == 0)
            return;

        Result<int> updateResult = await _playlistRepository.UpdateTrackPlaylistsAsync(
            target.Track,
            playlistIdsToAdd,
            playlistIdsToRemove);

        if (!updateResult.TryGetOk(out int changedCount))
        {
            await ShowToastSafeAsync(updateResult.Error.Message);
            return;
        }

        if (changedCount > 0)
        {
            await ShowToastSafeAsync("Плейлисты обновлены");
        }
    }

    private void OnMediaStoreTracksScanned(object? sender, IReadOnlyList<AudioTrack> scannedTracks)
    {
        _ = LoadTracksAsync();
    }

    private void OnRepositoryTrackFavoriteChanged(object? sender, TrackFavoriteChangedEventArgs e)
    {
        RunOnMainThread(() =>
        {
            if (_tracksByKey.TryGetValue(GetTrackKey(e.ContentUri), out TrackItemViewModel? item))
            {
                item.Track.SetFavorite(e.IsFavorite);
            }
        });
    }

    private async void OnStorageTrackAdded(object? sender, string rawFilePath)
    {
        try
        {
            string filePath = Path.GetFullPath(rawFilePath);

            TrackItemViewModel? newItem = await Task.Run(() =>
                TryCreateTrackItem(filePath, out var item) ? item : null);

            if (newItem is null)
                return;

            await _playlistRepository.SyncFavoritesWithTracksAsync([newItem.Track]);

            RunOnMainThread(() =>
            {
                RemoveTrackItemFromCollections(newItem.Track.ContentUri);
                lock (_loadedPaths)
                {
                    _loadedPaths.Add(filePath);
                }

                _tracksByKey[filePath] = newItem;
            _allTracks.Add(newItem);

                if (MatchesFilter(newItem, SearchQuery))
                {
                    FilteredTracks.Add(newItem);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TracksViewModel] OnStorageTrackAdded failed: {ex.Message}");
        }
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
            x.Track.ContentUri.IsAbsoluteUri && x.Track.ContentUri.IsFile &&
            string.Equals(Path.GetFullPath(x.Track.ContentUri.LocalPath), normalizedFilePath, StringComparison.OrdinalIgnoreCase));

        if (matching is not null)
        {
            RemoveTrackItemFromCollections(matching.Track.ContentUri);
        }
    }

    private void RemoveTrackItemFromCollections(Uri contentUri)
    {
        string key = GetTrackKey(contentUri);
        lock (_loadedPaths)
        {
            _loadedPaths.Remove(key);
            _loadedPaths.Remove(contentUri.OriginalString);
        }

        if (_tracksByKey.Remove(key, out TrackItemViewModel? removed))
        {
            _allTracks.Remove(removed);
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

    private static string GetTrackKey(Uri uri) =>
        (uri.IsAbsoluteUri && uri.IsFile) ? Path.GetFullPath(uri.LocalPath) : uri.OriginalString;

    private static bool AreSameTrackUri(Uri left, Uri right)
    {
        if (left == right)
            return true;

        if (left.IsAbsoluteUri && left.IsFile && right.IsAbsoluteUri && right.IsFile)
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

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _trackStorage.TrackAdded -= OnStorageTrackAdded;
        _trackStorage.TrackRemoved -= OnStorageTrackRemoved;
        _playlistRepository.TrackFavoriteChanged -= OnRepositoryTrackFavoriteChanged;
        if (_mediaStoreScanner is not null)
        {
            _mediaStoreScanner.TracksScanned -= OnMediaStoreTracksScanned;
        }
        _loadGate.Dispose();
    }
}
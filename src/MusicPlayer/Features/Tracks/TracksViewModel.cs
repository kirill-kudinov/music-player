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
using MusicPlayer.Features.Importer;
using MusicPlayer.Features.Player;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Tracks;

public sealed partial class TracksViewModel : ObservableObject, IDisposable
{
    private readonly TrackStorageSettings _storageSettings;
    private readonly TrackStorage _trackStorage;
    private readonly ITrackCoverExtractor _coverExtractor;
    private readonly IPlaybackQueue _playbackQueue;
    private readonly IAudioPlayer _audioPlayer;

    private readonly List<TrackItemViewModel> _allTracks = [];
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);
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
        PlayerViewModel playerViewModel)
    {
        _storageSettings = storageSettings;
        _trackStorage = trackStorage;
        _coverExtractor = coverExtractor;
        _playbackQueue = playbackQueue;
        _audioPlayer = audioPlayer;
        Player = playerViewModel;

        _trackStorage.TrackAdded += OnStorageTrackAdded;
        _trackStorage.TrackRemoved += OnStorageTrackRemoved;
    }

    partial void OnSearchQueryChanged(string value)
    {
        RunOnMainThread(() => ApplyFilter(value));
    }

    public void LoadTracks()
    {
        Player.Tick();

        if (!Directory.Exists(_storageSettings.BaseDirectory))
            return;

        bool hasChanges = false;

        foreach (string rawFilePath in Directory.EnumerateFiles(_storageSettings.BaseDirectory))
        {
            string filePath = Path.GetFullPath(rawFilePath);

            if (_trackStorage.IsExcluded(filePath) || !_loadedPaths.Add(filePath))
                continue;

            if (TryCreateTrackItem(filePath, out var trackItem))
            {
                _allTracks.Add(trackItem);
                hasChanges = true;
            }
        }

        if (hasChanges || (FilteredTracks.Count == 0 && _allTracks.Count > 0))
        {
            RunOnMainThread(() => ApplyFilter(SearchQuery));
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
        var target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;

        if (target is null)
            return;

        _playbackQueue.Enqueue(target.Track);
        Player.SyncQueueState();

        await ShowToastSafeAsync($"«{target.Track.Title}» добавлен в очередь");
    }

    [RelayCommand]
    private async Task AddTrackToPlaylistAsync()
    {
        var target = SelectedTrackForOptions;
        IsTrackOptionsPopupVisible = false;

        if (target is null)
            return;

        if (!target.Track.IsFavorite)
        {
            target.Track.ToggleFavorite();
            Player.SyncFavoriteState();
        }

        await ShowToastSafeAsync($"«{target.Track.Title}» добавлен в плейлист");
    }

    /// <summary>
    /// Мгновенно удаляет трек из списка UI, очереди воспроизведения и библиотеки плеера,
    /// не затрагивая исходный файл на физическом устройстве.
    /// </summary>
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

        await ShowToastSafeAsync($"«{target.Track.Title}» удалён из плеера");
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

    private void OnStorageTrackAdded(object? sender, string rawFilePath)
    {
        string filePath = Path.GetFullPath(rawFilePath);
        if (!TryCreateTrackItem(filePath, out var newItem))
            return;

        RunOnMainThread(() =>
        {
            RemoveTrackItemFromCollections(newItem.Track.ContentUri);

            _loadedPaths.Add(filePath);
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
        _loadedPaths.Remove(normalizedFilePath);

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
            _loadedPaths.Remove(Path.GetFullPath(contentUri.LocalPath));
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

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _trackStorage.TrackAdded -= OnStorageTrackAdded;
        _trackStorage.TrackRemoved -= OnStorageTrackRemoved;
    }
}
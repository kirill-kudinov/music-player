using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Importer;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Tracks;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Playlist;

public sealed partial class PlaylistTracksViewModel(
    TrackStorageSettings storageSettings,
    ITrackCoverExtractor coverExtractor,
    IPlaybackQueue playbackQueue,
    IAudioPlayer audioPlayer,
    Core.Playlist.Playlist playlist,
    PlayerViewModel playerViewModel) : ObservableObject
{
    private readonly List<TrackItemViewModel> _allTracks = [];
    private readonly HashSet<string> _loadedPaths = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    public PlayerViewModel Player { get; } = playerViewModel;

    public ObservableCollection<TrackItemViewModel> FilteredTracks { get; } = [];

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter(value);
    }

    public void LoadTracks()
    {
        Player.Tick();

        if (!Directory.Exists(storageSettings.BaseDirectory))
            return;

        bool hasNewTracks = false;

        foreach (string filePath in Directory.EnumerateFiles(storageSettings.BaseDirectory))
        {
            if (!_loadedPaths.Add(filePath))
                continue;

            var trackResult = AudioTrack.Create(filePath);
            if (!trackResult.TryGetOk(out var track))
                continue;

            var cover = coverExtractor.ExtractCover(track.ContentUri);
            _allTracks.Add(new TrackItemViewModel(track, cover));
            hasNewTracks = true;
        }

        if (hasNewTracks || FilteredTracks.Count == 0)
        {
            ApplyFilter(SearchQuery);
        }
    }

    [RelayCommand]
    private static void ToggleFavorite(TrackItemViewModel? item)
    {
        if (item is null)
            return;

        item.Track.ToggleFavorite();
    }

    [RelayCommand]
    private async Task OpenTrackAsync(TrackItemViewModel? item)
    {
        if (item is null)
            return;

        var queueTracks = _allTracks.Select(x => x.Track).ToList();
        playbackQueue.SetTracks(queueTracks, item.Track);
        audioPlayer.Play(item.Track.ContentUri);
        Player.NotifyTrackStarted();

        await Player.ExpandCommand.ExecuteAsync(null);
    }

    private void ApplyFilter(string? query)
    {
        FilteredTracks.Clear();

        string trimmed = query?.Trim() ?? string.Empty;
        IEnumerable<TrackItemViewModel> source = string.IsNullOrEmpty(trimmed)
            ? _allTracks
            : _allTracks.Where(x =>
                (x.Track.Title?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Track.Artist?.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ?? false));

        foreach (var item in source)
        {
            FilteredTracks.Add(item);
        }
    }
}
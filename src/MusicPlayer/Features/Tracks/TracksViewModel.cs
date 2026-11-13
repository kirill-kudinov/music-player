using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Features.Player;
using MusicPlayer.Infrastructure;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Tracks;

public sealed partial class TracksViewModel(
    Database database,
    ITrackCoverExtractor coverExtractor,
    IPlaybackQueue playbackQueue,
    IAudioPlayer audioPlayer,
    PlayerViewModel playerViewModel) : ObservableObject
{
    private readonly List<TrackItemViewModel> _allTracks = [];

    [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
    public PlayerViewModel Player { get; } = playerViewModel;

    public ObservableCollection<TrackItemViewModel> FilteredTracks { get; } = [];

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter(value);
    }

    public async Task LoadTracks()
    {
        Player.Tick();

        var hasNewTracks = false;
        
        _allTracks.Clear();

        foreach (var track in await database.GetAudioTracks())
        {
            var cover = coverExtractor.ExtractCover(track.ContentUri);
            _allTracks.Add(new TrackItemViewModel(track.Id, track, cover));
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
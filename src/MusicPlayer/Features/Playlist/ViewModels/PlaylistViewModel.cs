using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Playlist.Pages;
using MusicPlayer.Features.Tracks;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Playlist.ViewModels;

public sealed partial class PlaylistViewModel(
    ITrackCoverExtractor coverExtractor,
    IPlaybackQueue playbackQueue,
    IAudioPlayer audioPlayer,
    PlayerViewModel playerViewModel
) : ObservableObject
{
    private readonly List<TrackItemViewModel> _allTracks = [];

    public required Core.Playlist.Playlist Playlist { get; set; }

    [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
    public PlayerViewModel Player { get; } = playerViewModel;

    [ObservableProperty] public partial ObservableCollection<TrackItemViewModel> FilteredTracks { get; set; } = [];

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter(value);
    }

    public void LoadTracks()
    {
        Player.Tick();

        var hasNewTracks = false;

        _allTracks.Clear();

        foreach (var audioTrack in Playlist.Tracks)
        {
            var cover = coverExtractor.ExtractCover(audioTrack.ContentUri);
            _allTracks.Add(new TrackItemViewModel(audioTrack.Id, audioTrack, cover));
            hasNewTracks = true;
        }

        if (hasNewTracks || FilteredTracks.Count == 0)
        {
            ApplyFilter(SearchQuery);
        }
    }

    [RelayCommand]
    private async Task AddAudioTrack()
    {
        await Shell.Current.GoToAsync(
            $"{nameof(PlaylistChoosingTrackPage)}?PlaylistId={Playlist.Id}"
        );
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

        var trimmed = query?.Trim() ?? string.Empty;
        var source = string.IsNullOrEmpty(trimmed)
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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Playlist.Pages;
using MusicPlayer.Features.Tracks;
using MusicPlayer.Infrastructure;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Playlist.ViewModels;

public sealed partial class PlaylistChoosingTrackViewModel(
    Database database,
    ITrackCoverExtractor coverExtractor,
    PlayerViewModel playerViewModel) : ObservableObject
{
    private readonly List<TrackItemViewModel> _allTracks = [];

    public Core.Playlist.Playlist Playlist { get; set; }
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

    [RelayCommand]
    private async Task Choose(TrackItemViewModel audioTrack)
    {
        var foundAudioTrack = await database.GetAudioTrack(audioTrack.Id);
        await database.AddAudioTrackToPlaylist(foundAudioTrack, Playlist);
        await Shell.Current.GoToAsync($"//{nameof(PlaylistsPage)}");
    }
}
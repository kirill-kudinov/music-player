using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Tracks;
using MusicPlayer.Infrastructure;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Playlist.ViewModels;

public sealed partial class PlaylistChoosingTrackViewModel(
    Database database,
    ITrackCoverExtractor coverExtractor,
    PlayerViewModel playerViewModel) : ObservableObject
{
    public int PlaylistId { get; set; }
    private PlayerViewModel Player { get; } = playerViewModel;
    [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
    public ObservableCollection<TrackItemViewModel> FilteredTracks { get; } = [];
    private readonly List<TrackItemViewModel> _allTracks = [];

    [RelayCommand]
    private static void ToggleFavorite(TrackItemViewModel? item)
    {
        item?.Track.ToggleFavorite();
    }

    [RelayCommand]
    private async Task ChooseAsync(TrackItemViewModel audioTrack)
    {
        await database.AddAudioTrackToPlaylist(audioTrack.Id, PlaylistId);
        await Shell.Current.GoToAsync("..");
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter(value);
    }

    public async Task LoadTracksAsync()
    {
        Player.Tick();

        var hasNewTracks = false;

        foreach (var track in await database.GetAudioTracks())
        {
            var cover = coverExtractor.ExtractCover(track.ContentUri);
            _allTracks.Add(new TrackItemViewModel(track.Id, track, cover));
            hasNewTracks = true;
        }

        if (hasNewTracks || FilteredTracks.Count == 0) ApplyFilter(SearchQuery);
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

        foreach (var item in source) FilteredTracks.Add(item);
    }
}
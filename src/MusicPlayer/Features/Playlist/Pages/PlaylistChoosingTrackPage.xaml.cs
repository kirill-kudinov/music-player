using System.Collections.Generic;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Playlist.ViewModels;

namespace MusicPlayer.Features.Playlist.Pages;

public partial class PlaylistChoosingTrackPage : IQueryAttributable
{
    private readonly PlaylistChoosingTrackViewModel _playlistChoosingTrackViewModel;

    public PlaylistChoosingTrackPage(
        PlaylistChoosingTrackViewModel playlistChoosingTrackViewModel
    )
    {
        _playlistChoosingTrackViewModel = playlistChoosingTrackViewModel;

        InitializeComponent();

        BindingContext = playlistChoosingTrackViewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _playlistChoosingTrackViewModel.LoadTracks();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var playlist = query["Playlist"] as Core.Playlist.Playlist;
        _playlistChoosingTrackViewModel.Playlist = playlist;
    }
}
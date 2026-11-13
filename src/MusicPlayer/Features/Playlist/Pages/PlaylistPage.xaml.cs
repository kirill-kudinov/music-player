using System.Collections.Generic;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;
using MusicPlayer.Features.Playlist.ViewModels;

namespace MusicPlayer.Features.Playlist.Pages;

public partial class PlaylistPage : IQueryAttributable
{
    private readonly PlaylistViewModel _playlistViewModel;
    private readonly IPlayerTransitionService _transitionService;

    public PlaylistPage(
        PlaylistViewModel playlistViewModel,
        IPlayerTransitionService transitionService
    )
    {
        _playlistViewModel = playlistViewModel;
        _transitionService = transitionService;

        InitializeComponent();

        BindingContext = playlistViewModel;

        transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query["Playlist"] is not Core.Playlist.Playlist playlist) return;

        _playlistViewModel.Playlist = playlist;
        _playlistViewModel.LoadTracks(_playlistViewModel.Playlist);
    }
}
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Playlists;

public partial class PlaylistsPage : ContentPage
{
    private readonly PlaylistsViewModel _playlistsViewModel;
    private readonly IPlayerTransitionService _transitionService;

    public PlaylistsPage(
        PlaylistsViewModel playlistsViewModel,
        IPlayerTransitionService transitionService)
    {
        _playlistsViewModel = playlistsViewModel;
        _transitionService = transitionService;

        InitializeComponent();

        BindingContext = playlistsViewModel;

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);

        _playlistsViewModel.LoadPlaylists();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);

        _playlistsViewModel.LoadPlaylists();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_playlistsViewModel.IsEditorModalVisible)
        {
            _playlistsViewModel.CloseEditorModalCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}

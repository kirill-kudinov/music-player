using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Playlists;

public partial class PlaylistDetailPage : ContentPage
{
    private readonly PlaylistDetailViewModel _viewModel;
    private readonly IPlayerTransitionService _transitionService;

    public PlaylistDetailPage(
        PlaylistDetailViewModel viewModel,
        IPlayerTransitionService transitionService)
    {
        _viewModel = viewModel;
        _transitionService = transitionService;

        InitializeComponent();

        BindingContext = viewModel;

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);

        _viewModel.RefreshIfLoaded();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsAddTracksSheetVisible)
        {
            _viewModel.CloseAddTracksSheetCommand.Execute(null);
            return true;
        }

        if (_viewModel.IsTrackOptionsPopupVisible)
        {
            _viewModel.CloseTrackOptionsCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}

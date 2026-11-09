using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Tracks;

public partial class TracksPage : ContentPage
{
    private readonly TracksViewModel _tracksViewModel;
    private readonly IPlayerTransitionService _transitionService;

    public TracksPage(
        TracksViewModel tracksViewModel,
        IPlayerTransitionService transitionService)
    {
        _tracksViewModel = tracksViewModel;
        _transitionService = transitionService;

        InitializeComponent();

        BindingContext = tracksViewModel;

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);

        _tracksViewModel.LoadTracks();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);

        _tracksViewModel.LoadTracks();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_tracksViewModel.IsTrackOptionsPopupVisible)
        {
            _tracksViewModel.CloseTrackOptionsCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
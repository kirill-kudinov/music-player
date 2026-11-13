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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);

        await _tracksViewModel.LoadTracks();
    }
}
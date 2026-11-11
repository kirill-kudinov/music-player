using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Player;

public partial class PlayerPage : ContentPage
{
    private const double CollapseDragThreshold = 110.0;

    private readonly PlayerViewModel _viewModel;
    private readonly IPlayerTransitionService _transitionService;
    private readonly IDispatcherTimer _pollingTimer;
    private bool _hasPlayedExpandAnimation;
    private bool _isDraggingSheet;

    public PlayerPage(
        PlayerViewModel viewModel,
        IPlayerTransitionService transitionService)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _transitionService = transitionService;

        _pollingTimer = Dispatcher.CreateTimer();
        _pollingTimer.Interval = TimeSpan.FromMilliseconds(100);
        _pollingTimer.Tick += OnPollingTimerTick;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _transitionService.RegisterPlayerPageCallbacks(PlayCollapseAnimationAsync);
        _viewModel.Tick();

        if (!_hasPlayedExpandAnimation)
        {
            _hasPlayedExpandAnimation = true;

            PlayerVisualAnimator.PrepareForExpand(
                PlayerRootGrid,
                HeaderSection,
                CoverSection,
                TrackInfoSection,
                ControlsSection,
                Height);

            await PlayerVisualAnimator.AnimateExpandAsync(
                PlayerRootGrid,
                HeaderSection,
                CoverSection,
                TrackInfoSection,
                ControlsSection);
        }

        _pollingTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pollingTimer.Stop();
        _transitionService.UnregisterPlayerPageCallbacks();
        _hasPlayedExpandAnimation = false;
        _isDraggingSheet = false;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.CollapseCommand.CanExecute(null))
        {
            _viewModel.CollapseCommand.Execute(null);
        }

        return true;
    }

    private Task PlayCollapseAnimationAsync()
    {
        _pollingTimer.Stop();
        return PlayerVisualAnimator.AnimateCollapseAsync(
            PlayerRootGrid,
            HeaderSection,
            CoverSection,
            TrackInfoSection,
            ControlsSection,
            Height);
    }

    private async void OnCollapsePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_transitionService.IsTransitioning)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _isDraggingSheet = true;
                PlayerRootGrid.CancelAnimations();
                break;

            case GestureStatus.Running:
                if (e.TotalY <= 0 || Math.Abs(e.TotalY) < Math.Abs(e.TotalX))
                    return;

                double viewportHeight = Height > 100 ? Height : 800.0;
                double progress = Math.Clamp(e.TotalY / (viewportHeight * 0.55), 0.0, 1.0);

                PlayerRootGrid.TranslationY = e.TotalY;
                PlayerRootGrid.Scale = 1.0 - (progress * 0.08);
                PlayerRootGrid.Opacity = 1.0 - (progress * 0.35);
                CoverSection.Scale = 1.0 - (progress * 0.30);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isDraggingSheet = false;
                if (PlayerRootGrid.TranslationY >= CollapseDragThreshold)
                {
                    await _viewModel.CollapseCommand.ExecuteAsync(null);
                }
                else
                {
                    await PlayerVisualAnimator.AnimateSnapBackAsync(PlayerRootGrid, CoverSection);
                }
                break;
        }
    }

    private void OnPollingTimerTick(object? sender, EventArgs e)
    {
        if (_isDraggingSheet)
            return;

        _viewModel.Tick();
    }
}
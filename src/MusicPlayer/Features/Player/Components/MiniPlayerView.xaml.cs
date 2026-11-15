using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Player.Components;

public partial class MiniPlayerView : ContentView
{
    private const string MiniPlayerGestureAnimationName = "MiniPlayerGestureTransition";
    private const uint FrameRateMs = 8;
    private const uint DismissAnimationMs = 160;
    private const uint SnapBackAnimationMs = 150;
    private const double DismissDragThreshold = 34.0;
    private const double ExpandDragThreshold = -42.0;
    private const double DismissTargetOffsetY = 92.0;

    private bool _isDismissing;
    private double _lastPanY;

    public MiniPlayerView()
    {
        InitializeComponent();
        PropertyChanged += OnMiniPlayerPropertyChanged;
    }

    public Task PlayExpandOutAnimationAsync()
    {
        return PlayerVisualAnimator.AnimateMiniPlayerExpandOutAsync(MiniPlayerContainer);
    }

    public Task PlayCollapseInAnimationAsync()
    {
        MiniPlayerContainer.TranslationY = 0.0;
        MiniPlayerContainer.Opacity = 1.0;
        return PlayerVisualAnimator.AnimateMiniPlayerCollapseInAsync(MiniPlayerContainer);
    }

    private void OnMiniPlayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsVisible) && IsVisible)
        {
            _isDismissing = false;
            MiniPlayerContainer.AbortAnimation(MiniPlayerGestureAnimationName);
            MiniPlayerContainer.TranslationY = 0.0;
            MiniPlayerContainer.Opacity = 1.0;
            AndroidRenderLayerHelper.SetHardwareLayer(MiniPlayerContainer, false);
        }
    }

    private void OnMiniPlayerPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_isDismissing || !IsVisible)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _lastPanY = 0.0;
                MiniPlayerContainer.AbortAnimation(MiniPlayerGestureAnimationName);
                AndroidRenderLayerHelper.SetHardwareLayer(MiniPlayerContainer, true);
                break;

            case GestureStatus.Running:
                _lastPanY = e.TotalY;
                if (e.TotalY >= 0.0)
                {
                    double offsetY = e.TotalY;
                    double progress = Math.Clamp(offsetY / DismissTargetOffsetY, 0.0, 1.0);
                    MiniPlayerContainer.TranslationY = offsetY;
                    MiniPlayerContainer.Opacity = 1.0 - (progress * 0.85);
                }
                else
                {
                    MiniPlayerContainer.TranslationY = Math.Max(-14.0, e.TotalY * 0.25);
                    MiniPlayerContainer.Opacity = 1.0;
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (MiniPlayerContainer.TranslationY >= DismissDragThreshold)
                {
                    AnimateDismissOut();
                }
                else if (_lastPanY <= ExpandDragThreshold)
                {
                    AnimateSnapBack();
                    if (BindingContext is PlayerViewModel vm && vm.ExpandCommand.CanExecute(null))
                    {
                        vm.ExpandCommand.Execute(null);
                    }
                }
                else
                {
                    AnimateSnapBack();
                }
                break;
        }
    }

    private void AnimateDismissOut()
    {
        _isDismissing = true;
        MiniPlayerContainer.AbortAnimation(MiniPlayerGestureAnimationName);
        AndroidRenderLayerHelper.SetHardwareLayer(MiniPlayerContainer, true);

        double startY = MiniPlayerContainer.TranslationY;
        double startOpacity = MiniPlayerContainer.Opacity;

        var dismissAnimation = new Animation
        {
            { 0.0, 1.0, new Animation(v => MiniPlayerContainer.TranslationY = v, startY, DismissTargetOffsetY, Easing.CubicIn) },
            { 0.0, 1.0, new Animation(v => MiniPlayerContainer.Opacity = v, startOpacity, 0.0, Easing.CubicIn) }
        };

        dismissAnimation.Commit(
            owner: MiniPlayerContainer,
            name: MiniPlayerGestureAnimationName,
            rate: FrameRateMs,
            length: DismissAnimationMs,
            easing: Easing.Linear,
            finished: (_, _) =>
            {
                AndroidRenderLayerHelper.SetHardwareLayer(MiniPlayerContainer, false);

                if (BindingContext is PlayerViewModel vm && vm.DismissMiniPlayerCommand.CanExecute(null))
                {
                    vm.DismissMiniPlayerCommand.Execute(null);
                }

                MiniPlayerContainer.TranslationY = 0.0;
                MiniPlayerContainer.Opacity = 1.0;
                _isDismissing = false;
            });
    }

    private void AnimateSnapBack()
    {
        MiniPlayerContainer.AbortAnimation(MiniPlayerGestureAnimationName);

        double startY = MiniPlayerContainer.TranslationY;
        double startOpacity = MiniPlayerContainer.Opacity;

        var snapAnimation = new Animation
        {
            { 0.0, 1.0, new Animation(v => MiniPlayerContainer.TranslationY = v, startY, 0.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => MiniPlayerContainer.Opacity = v, startOpacity, 1.0, Easing.CubicOut) }
        };

        snapAnimation.Commit(
            owner: MiniPlayerContainer,
            name: MiniPlayerGestureAnimationName,
            rate: FrameRateMs,
            length: SnapBackAnimationMs,
            easing: Easing.Linear,
            finished: (_, _) => AndroidRenderLayerHelper.SetHardwareLayer(MiniPlayerContainer, false));
    }
}
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Tracks.Components;

/// <summary>
/// Нижняя шторка (Bottom Sheet) действий с треком в стиле YouTube Music.
/// Использует нативный Android ViewPropertyAnimator (RenderThread) без нагрузки на C# UI-поток.
/// </summary>
public partial class TrackOptionsBottomSheet : ContentView
{
    private const string SheetSnapAnimationName = "TrackOptionsBottomSheetSnap";
    private const uint OpenDurationMs = 220;
    private const uint CloseDurationMs = 170;
    private const uint SnapBackDurationMs = 150;
    private const double DefaultHiddenOffsetY = 420.0;
    private const double DismissDragThreshold = 90.0;

    private bool _isActionExecuting;
    private int _animationGeneration;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(TrackOptionsBottomSheet),
        defaultValue: false,
        propertyChanged: OnIsOpenPropertyChanged);

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public TrackOptionsBottomSheet()
    {
        InitializeComponent();
    }

    private static void OnIsOpenPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TrackOptionsBottomSheet sheet && newValue is bool isOpen)
        {
            sheet.AnimateSheetState(isOpen);
        }
    }

    private void AnimateSheetState(bool isOpen)
    {
        int generation = ++_animationGeneration;
        this.AbortAnimation(SheetSnapAnimationName);
        NativeSheetAnimator.Cancel(BottomSheetContainer);
        NativeSheetAnimator.Cancel(BackdropScrim);
        _isActionExecuting = false;

        double hiddenOffsetY = ResolveSheetHeight();

        if (isOpen)
        {
            BackdropScrim.Opacity = 0.0;
            BottomSheetContainer.TranslationY = hiddenOffsetY;
            BottomSheetContainer.Opacity = 1.0;

            IsVisible = true;
            InputTransparent = false;

            NativeSheetAnimator.PostAfterLayout(BottomSheetContainer, () =>
            {
                if (!IsOpen || generation != _animationGeneration)
                    return;

                double startOffsetY = ResolveSheetHeight();
                NativeSheetAnimator.AnimateSlideFade(
                    BackdropScrim,
                    0.0, 0.0,
                    0.0, 1.0,
                    OpenDurationMs,
                    isOpening: true,
                    useHardwareLayer: false);

                NativeSheetAnimator.AnimateSlideFade(
                    BottomSheetContainer,
                    startOffsetY, 0.0,
                    1.0, 1.0,
                    OpenDurationMs,
                    isOpening: true);
            });
        }
        else
        {
            InputTransparent = true;

            double startScrimOpacity = BackdropScrim.Opacity;
            double startSheetY = BottomSheetContainer.TranslationY;

            NativeSheetAnimator.AnimateSlideFade(
                BackdropScrim,
                0.0, 0.0,
                startScrimOpacity, 0.0,
                CloseDurationMs,
                isOpening: false,
                useHardwareLayer: false);

            NativeSheetAnimator.AnimateSlideFade(
                BottomSheetContainer,
                startSheetY, hiddenOffsetY,
                1.0, 1.0,
                CloseDurationMs,
                isOpening: false,
                onCompleted: () =>
                {
                    if (!IsOpen && generation == _animationGeneration)
                    {
                        IsVisible = false;
                    }
                });
        }
    }

    private void OnSheetPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!IsOpen)
            return;

        double sheetHeight = ResolveSheetHeight();

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                this.AbortAnimation(SheetSnapAnimationName);
                NativeSheetAnimator.Cancel(BottomSheetContainer);
                NativeSheetAnimator.Cancel(BackdropScrim);
                break;

            case GestureStatus.Running:
                double offsetY = Math.Max(0.0, e.TotalY);
                double progress = Math.Clamp(offsetY / sheetHeight, 0.0, 1.0);

                BottomSheetContainer.TranslationY = offsetY;
                BackdropScrim.Opacity = 1.0 - (progress * 0.85);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (BottomSheetContainer.TranslationY >= DismissDragThreshold)
                {
                    if (BindingContext is TracksViewModel vm && vm.CloseTrackOptionsCommand.CanExecute(null))
                    {
                        vm.CloseTrackOptionsCommand.Execute(null);
                    }
                }
                else
                {
                    AnimateSnapBack();
                }
                break;
        }
    }

    private void AnimateSnapBack()
    {
        NativeSheetAnimator.AnimateSlideFade(
            BackdropScrim,
            0.0, 0.0,
            BackdropScrim.Opacity, 1.0,
            SnapBackDurationMs,
            isOpening: true);

        NativeSheetAnimator.AnimateSlideFade(
            BottomSheetContainer,
            BottomSheetContainer.TranslationY, 0.0,
            1.0, 1.0,
            SnapBackDurationMs,
            isOpening: true);
    }

    private async void OnAddToQueueTapped(object? sender, TappedEventArgs e)
    {
        if (!IsOpen || _isActionExecuting)
            return;

        _isActionExecuting = true;
        await PlayRowTapFeedbackAsync(QueueActionRow);
        if (IsOpen && BindingContext is TracksViewModel vm)
        {
            ExecuteCommandIfPossible(vm.AddTrackToQueueCommand);
        }
    }

    private async void OnAddToPlaylistTapped(object? sender, TappedEventArgs e)
    {
        if (!IsOpen || _isActionExecuting)
            return;

        _isActionExecuting = true;
        await PlayRowTapFeedbackAsync(PlaylistActionRow);
        if (IsOpen && BindingContext is TracksViewModel vm)
        {
            ExecuteCommandIfPossible(vm.AddTrackToPlaylistCommand);
        }
    }

    private async void OnDeleteTrackTapped(object? sender, TappedEventArgs e)
    {
        if (!IsOpen || _isActionExecuting)
            return;

        _isActionExecuting = true;
        await PlayRowTapFeedbackAsync(DeleteActionRow);
        if (IsOpen && BindingContext is TracksViewModel vm)
        {
            ExecuteCommandIfPossible(vm.DeleteTrackFromPlayerCommand);
        }
    }

    private static async Task PlayRowTapFeedbackAsync(VisualElement row)
    {
        await row.ScaleToAsync(0.97, 45, Easing.CubicOut);
        await row.ScaleToAsync(1.0, 55, Easing.CubicIn);
    }

    private static void ExecuteCommandIfPossible(ICommand? command)
    {
        if (command is not null && command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private double ResolveSheetHeight()
    {
        return BottomSheetContainer.Height > 80.0 ? BottomSheetContainer.Height : DefaultHiddenOffsetY;
    }
}
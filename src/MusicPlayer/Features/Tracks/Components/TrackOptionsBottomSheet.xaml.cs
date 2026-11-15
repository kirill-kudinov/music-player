using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Tracks.Components;

/// <summary>
/// Нижняя шторка (Bottom Sheet) действий с треком в стиле YouTube Music.
/// Выдвигается снизу экрана с использованием аппаратного GPU-слоя Android (LayerType.Hardware)
/// и аппаратно-ускоренных свойств (TranslationY, Opacity), поддерживает закрытие свайпом вниз.
/// </summary>
public partial class TrackOptionsBottomSheet : ContentView
{
    private const string SheetAnimationName = "TrackOptionsBottomSheetTransition";
    private const string SheetSnapAnimationName = "TrackOptionsBottomSheetSnap";
    private const uint FrameRateMs = 8;
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
        this.AbortAnimation(SheetAnimationName);
        this.AbortAnimation(SheetSnapAnimationName);
        _isActionExecuting = false;

        double hiddenOffsetY = ResolveSheetHeight();

        if (isOpen)
        {
            BackdropScrim.Opacity = 0.0;
            BottomSheetContainer.TranslationY = hiddenOffsetY;

            IsVisible = true;
            InputTransparent = false;

            Dispatcher.Dispatch(() =>
            {
                if (!IsOpen || generation != _animationGeneration)
                    return;

                double startOffsetY = ResolveSheetHeight();
                BottomSheetContainer.TranslationY = startOffsetY;

                AndroidRenderLayerHelper.SetHardwareLayer(BottomSheetContainer, true);
                AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, true);

                var openAnimation = new Animation
                {
                    { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
                    { 0.0, 1.0, new Animation(v => BottomSheetContainer.TranslationY = v, startOffsetY, 0.0, Easing.CubicOut) }
                };

                openAnimation.Commit(
                    owner: this,
                    name: SheetAnimationName,
                    rate: FrameRateMs,
                    length: OpenDurationMs,
                    easing: Easing.Linear,
                    finished: (_, _) =>
                    {
                        AndroidRenderLayerHelper.SetHardwareLayer(BottomSheetContainer, false);
                        AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, false);
                    });
            });
        }
        else
        {
            InputTransparent = true;

            double startScrimOpacity = BackdropScrim.Opacity;
            double startSheetY = BottomSheetContainer.TranslationY;

            AndroidRenderLayerHelper.SetHardwareLayer(BottomSheetContainer, true);
            AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, true);

            var closeAnimation = new Animation
            {
                { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, startScrimOpacity, 0.0, Easing.CubicIn) },
                { 0.0, 1.0, new Animation(v => BottomSheetContainer.TranslationY = v, startSheetY, hiddenOffsetY, Easing.CubicIn) }
            };

            closeAnimation.Commit(
                owner: this,
                name: SheetAnimationName,
                rate: FrameRateMs,
                length: CloseDurationMs,
                easing: Easing.Linear,
                finished: (_, _) =>
                {
                    AndroidRenderLayerHelper.SetHardwareLayer(BottomSheetContainer, false);
                    AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, false);

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
                this.AbortAnimation(SheetAnimationName);
                this.AbortAnimation(SheetSnapAnimationName);
                AndroidRenderLayerHelper.SetHardwareLayer(BottomSheetContainer, true);
                AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, true);
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
        this.AbortAnimation(SheetSnapAnimationName);
        AndroidRenderLayerHelper.SetHardwareLayer(BottomSheetContainer, true);
        AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, true);

        var snapAnimation = new Animation
        {
            { 0.0, 1.0, new Animation(v => BottomSheetContainer.TranslationY = v, BottomSheetContainer.TranslationY, 0.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, BackdropScrim.Opacity, 1.0, Easing.CubicOut) }
        };

        snapAnimation.Commit(
            owner: this,
            name: SheetSnapAnimationName,
            rate: FrameRateMs,
            length: SnapBackDurationMs,
            easing: Easing.Linear,
            finished: (_, _) =>
            {
                AndroidRenderLayerHelper.SetHardwareLayer(BottomSheetContainer, false);
                AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, false);
            });
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
        await row.ScaleToAsync(0.97, 50, Easing.CubicOut);
        await row.ScaleToAsync(1.0, 65, Easing.CubicIn);
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
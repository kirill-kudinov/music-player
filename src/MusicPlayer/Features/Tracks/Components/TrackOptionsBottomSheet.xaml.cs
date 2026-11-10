using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace MusicPlayer.Features.Tracks.Components;

/// <summary>
/// Нижняя шторка (Bottom Sheet) действий с треком в стиле YouTube Music.
/// Выдвигается снизу экрана с использованием аппаратно-ускоренных свойств RenderNode (TranslationY, Opacity),
/// поддерживает интерактивное закрытие свайпом вниз (PanGestureRecognizer) и не блокирует UI-поток.
/// </summary>
public partial class TrackOptionsBottomSheet : ContentView
{
    private const string SheetAnimationName = "TrackOptionsBottomSheetTransition";
    private const string SheetSnapAnimationName = "TrackOptionsBottomSheetSnap";
    private const uint FrameRateHz = 16;
    private const uint OpenDurationMs = 240;
    private const uint CloseDurationMs = 190;
    private const uint SnapBackDurationMs = 160;
    private const double DefaultHiddenOffsetY = 420.0;
    private const double DismissDragThreshold = 90.0;

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
        this.AbortAnimation(SheetAnimationName);
        this.AbortAnimation(SheetSnapAnimationName);

        double hiddenOffsetY = ResolveSheetHeight();

        if (isOpen)
        {
            IsVisible = true;
            InputTransparent = false;

            BackdropScrim.Opacity = 0.0;
            BottomSheetContainer.TranslationY = hiddenOffsetY;

            var openAnimation = new Animation
            {
                { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
                { 0.0, 1.0, new Animation(v => BottomSheetContainer.TranslationY = v, hiddenOffsetY, 0.0, Easing.CubicOut) }
            };

            openAnimation.Commit(
                owner: this,
                name: SheetAnimationName,
                rate: FrameRateHz,
                length: OpenDurationMs,
                easing: Easing.Linear);
        }
        else
        {
            InputTransparent = true;

            double startScrimOpacity = BackdropScrim.Opacity;
            double startSheetY = BottomSheetContainer.TranslationY;

            var closeAnimation = new Animation
            {
                { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, startScrimOpacity, 0.0, Easing.CubicIn) },
                { 0.0, 1.0, new Animation(v => BottomSheetContainer.TranslationY = v, startSheetY, hiddenOffsetY, Easing.CubicIn) }
            };

            closeAnimation.Commit(
                owner: this,
                name: SheetAnimationName,
                rate: FrameRateHz,
                length: CloseDurationMs,
                easing: Easing.Linear,
                finished: (_, _) =>
                {
                    if (!IsOpen)
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

        var snapAnimation = new Animation
        {
            { 0.0, 1.0, new Animation(v => BottomSheetContainer.TranslationY = v, BottomSheetContainer.TranslationY, 0.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, BackdropScrim.Opacity, 1.0, Easing.CubicOut) }
        };

        snapAnimation.Commit(
            owner: this,
            name: SheetSnapAnimationName,
            rate: FrameRateHz,
            length: SnapBackDurationMs,
            easing: Easing.Linear);
    }

    private async void OnAddToQueueTapped(object? sender, TappedEventArgs e)
    {
        await PlayRowTapFeedbackAsync(QueueActionRow);
        if (BindingContext is TracksViewModel vm)
        {
            ExecuteCommandIfPossible(vm.AddTrackToQueueCommand);
        }
    }

    private async void OnAddToPlaylistTapped(object? sender, TappedEventArgs e)
    {
        await PlayRowTapFeedbackAsync(PlaylistActionRow);
        if (BindingContext is TracksViewModel vm)
        {
            ExecuteCommandIfPossible(vm.AddTrackToPlaylistCommand);
        }
    }

    private async void OnDeleteTrackTapped(object? sender, TappedEventArgs e)
    {
        await PlayRowTapFeedbackAsync(DeleteActionRow);
        if (BindingContext is TracksViewModel vm)
        {
            ExecuteCommandIfPossible(vm.DeleteTrackFromPlayerCommand);
        }
    }

    private static async Task PlayRowTapFeedbackAsync(VisualElement row)
    {
        await row.ScaleTo(0.97, 60, Easing.CubicOut);
        await row.ScaleTo(1.0, 80, Easing.CubicIn);
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
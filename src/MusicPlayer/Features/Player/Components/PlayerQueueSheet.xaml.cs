using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Player.Components;

public partial class PlayerQueueSheet : ContentView
{
    private const string QueueAnimationName = "PlayerQueueSheetTransition";
    private const uint FrameRateMs = 8;
    private const uint OpenDurationMs = 220;
    private const uint CloseDurationMs = 170;
    private const double HiddenTranslationY = 56.0;
    private const double RowStepHeight = 70.0;
    private const double SwapThreshold = 35.0;

    private int _animationGeneration;
    private Border? _draggedRow;
    private QueueTrackItemViewModel? _draggedItem;
    private double _accumulatedSwapOffset;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(PlayerQueueSheet),
        defaultValue: false,
        propertyChanged: OnIsOpenPropertyChanged);

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public PlayerQueueSheet()
    {
        InitializeComponent();
    }

    private static void OnIsOpenPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is PlayerQueueSheet sheet && newValue is bool isOpen)
        {
            sheet.AnimateSheetState(isOpen);
        }
    }

    private void AnimateSheetState(bool isOpen)
    {
        int generation = ++_animationGeneration;
        this.AbortAnimation(QueueAnimationName);

        if (isOpen)
        {
            SheetRoot.Opacity = 0.0;
            SheetRoot.TranslationY = HiddenTranslationY;

            IsVisible = true;
            InputTransparent = false;

            Dispatcher.Dispatch(() =>
            {
                if (!IsOpen || generation != _animationGeneration)
                    return;

                AndroidRenderLayerHelper.SetHardwareLayer(SheetRoot, true);

                var openAnimation = new Animation
                {
                    { 0.0, 0.85, new Animation(v => SheetRoot.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
                    { 0.0, 1.0, new Animation(v => SheetRoot.TranslationY = v, HiddenTranslationY, 0.0, Easing.CubicOut) }
                };

                openAnimation.Commit(
                    owner: this,
                    name: QueueAnimationName,
                    rate: FrameRateMs,
                    length: OpenDurationMs,
                    easing: Easing.Linear,
                    finished: (_, _) => AndroidRenderLayerHelper.SetHardwareLayer(SheetRoot, false));
            });
        }
        else
        {
            InputTransparent = true;

            double startOpacity = SheetRoot.Opacity;
            double startY = SheetRoot.TranslationY;

            AndroidRenderLayerHelper.SetHardwareLayer(SheetRoot, true);

            var closeAnimation = new Animation
            {
                { 0.0, 1.0, new Animation(v => SheetRoot.Opacity = v, startOpacity, 0.0, Easing.CubicIn) },
                { 0.0, 1.0, new Animation(v => SheetRoot.TranslationY = v, startY, HiddenTranslationY, Easing.CubicIn) }
            };

            closeAnimation.Commit(
                owner: this,
                name: QueueAnimationName,
                rate: FrameRateMs,
                length: CloseDurationMs,
                easing: Easing.Linear,
                finished: (_, _) =>
                {
                    AndroidRenderLayerHelper.SetHardwareLayer(SheetRoot, false);

                    if (!IsOpen && generation == _animationGeneration)
                    {
                        IsVisible = false;
                    }
                });
        }
    }

    private void OnDragHandleHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (sender is VisualElement element && element.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.Touch -= OnAndroidDragHandleTouch;
            nativeView.Touch += OnAndroidDragHandleTouch;
        }
#endif
    }

#if ANDROID
    private static void OnAndroidDragHandleTouch(object? sender, Android.Views.View.TouchEventArgs e)
    {
        if (sender is Android.Views.View view && e.Event?.ActionMasked == Android.Views.MotionEventActions.Down)
        {
            view.Parent?.RequestDisallowInterceptTouchEvent(true);
        }

        e.Handled = false;
    }
#endif

    private void OnDragHandlePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (BindingContext is not PlayerViewModel vm || sender is not Element handle)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _draggedRow = FindParentBorder(handle);
                _draggedItem = _draggedRow?.BindingContext as QueueTrackItemViewModel;
                _accumulatedSwapOffset = 0.0;

                if (_draggedRow is not null)
                {
                    _draggedRow.ZIndex = 20;
                    _draggedRow.Scale = 1.02;
                    _draggedRow.Opacity = 0.92;
                }
                break;

            case GestureStatus.Running:
                if (_draggedRow is null || _draggedItem is null)
                    return;

                int currentIndex = vm.QueueTracks.IndexOf(_draggedItem);
                if (currentIndex < 0)
                    return;

                double deltaY = e.TotalY - _accumulatedSwapOffset;

                while (deltaY >= SwapThreshold && currentIndex < vm.QueueTracks.Count - 1)
                {
                    int targetIndex = currentIndex + 1;
                    vm.MoveQueueItem(currentIndex, targetIndex);
                    currentIndex = targetIndex;
                    _accumulatedSwapOffset += RowStepHeight;
                    deltaY = e.TotalY - _accumulatedSwapOffset;
                }

                while (deltaY <= -SwapThreshold && currentIndex > 0)
                {
                    int targetIndex = currentIndex - 1;
                    vm.MoveQueueItem(currentIndex, targetIndex);
                    currentIndex = targetIndex;
                    _accumulatedSwapOffset -= RowStepHeight;
                    deltaY = e.TotalY - _accumulatedSwapOffset;
                }

                _draggedRow.TranslationY = Math.Clamp(deltaY, -RowStepHeight, RowStepHeight);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (_draggedRow is not null)
                {
                    _draggedRow.TranslationY = 0.0;
                    _draggedRow.Scale = 1.0;
                    _draggedRow.Opacity = 1.0;
                    _draggedRow.ZIndex = 0;
                }

                _draggedRow = null;
                _draggedItem = null;
                _accumulatedSwapOffset = 0.0;
                break;
        }
    }

    private async void OnQueueItemMoreTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is not PlayerViewModel vm ||
            e.Parameter is not QueueTrackItemViewModel item ||
            Shell.Current?.CurrentPage is null)
        {
            return;
        }

        string? choice = await Shell.Current.CurrentPage.DisplayActionSheetAsync(
            item.Track.Title,
            "Отмена",
            "Убрать из очереди",
            "Воспроизвести");

        if (choice == "Убрать из очереди" && vm.RemoveFromQueueCommand.CanExecute(item))
        {
            vm.RemoveFromQueueCommand.Execute(item);
        }
        else if (choice == "Воспроизвести" && vm.PlayQueueItemCommand.CanExecute(item))
        {
            vm.PlayQueueItemCommand.Execute(item);
        }
    }

    private static Border? FindParentBorder(Element element)
    {
        Element? current = element.Parent;
        while (current is not null)
        {
            if (current is Border border && border.BindingContext is QueueTrackItemViewModel)
                return border;

            current = current.Parent;
        }

        return null;
    }
}

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
    private const double SwipeRemoveThreshold = -85.0;

    private int _animationGeneration;
    private Border? _draggedRow;
    private QueueTrackItemViewModel? _draggedItem;
    private double _accumulatedSwapOffset;
    private bool _isShufflingAnimation;

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

                int currentIndex = vm.Queue.Tracks.IndexOf(_draggedItem);
                if (currentIndex < 0)
                    return;

                double deltaY = e.TotalY - _accumulatedSwapOffset;

                while (deltaY >= SwapThreshold && currentIndex < vm.Queue.Tracks.Count - 1)
                {
                    int targetIndex = currentIndex + 1;
                    vm.Queue.MoveItem(currentIndex, targetIndex);
                    currentIndex = targetIndex;
                    _accumulatedSwapOffset += RowStepHeight;
                    deltaY = e.TotalY - _accumulatedSwapOffset;
                }

                while (deltaY <= -SwapThreshold && currentIndex > 0)
                {
                    int targetIndex = currentIndex - 1;
                    vm.Queue.MoveItem(currentIndex, targetIndex);
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

    private async void OnRowSwipePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (BindingContext is not PlayerViewModel vm ||
            sender is not Border row ||
            row.BindingContext is not QueueTrackItemViewModel item)
        {
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                row.CancelAnimations();
                AndroidRenderLayerHelper.SetHardwareLayer(row, true);
                break;

            case GestureStatus.Running:
                if (e.TotalX >= 0 || Math.Abs(e.TotalX) < Math.Abs(e.TotalY))
                    return;

                double offsetX = Math.Max(-260.0, e.TotalX);
                row.TranslationX = offsetX;
                row.Opacity = Math.Clamp(1.0 + (offsetX / 220.0), 0.2, 1.0);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (row.TranslationX <= SwipeRemoveThreshold)
                {
                    await System.Threading.Tasks.Task.WhenAll(
                        row.TranslateToAsync(-360.0, 0.0, 140, Easing.CubicIn),
                        row.FadeToAsync(0.0, 140, Easing.CubicIn));

                    AndroidRenderLayerHelper.SetHardwareLayer(row, false);
                    row.TranslationX = 0.0;
                    row.Opacity = 1.0;

                    if (vm.Queue.RemoveItemCommand.CanExecute(item))
                    {
                        vm.Queue.RemoveItemCommand.Execute(item);
                    }
                }
                else
                {
                    await System.Threading.Tasks.Task.WhenAll(
                        row.TranslateToAsync(0.0, 0.0, 140, Easing.CubicOut),
                        row.FadeToAsync(1.0, 140, Easing.CubicOut));

                    AndroidRenderLayerHelper.SetHardwareLayer(row, false);
                }
                break;
        }
    }

    private void OnQueueCollectionViewHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        if (QueueCollectionView.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recyclerView &&
            recyclerView.GetItemAnimator() is { } animator)
        {
            animator.MoveDuration = 280;
            animator.ChangeDuration = 180;
        }
#endif
    }

    private async void OnShuffleButtonTapped(object? sender, TappedEventArgs e)
    {
        if (_isShufflingAnimation || BindingContext is not PlayerViewModel vm)
            return;

        _isShufflingAnimation = true;
        try
        {
            if (vm.ToggleShuffleCommand.CanExecute(null))
            {
                vm.ToggleShuffleCommand.Execute(null);
            }

            await System.Threading.Tasks.Task.WhenAll(
                ShuffleIcon.RotateToAsync(360.0, 260, Easing.CubicOut),
                ShuffleIcon.ScaleToAsync(1.15, 120, Easing.CubicOut));

            await ShuffleIcon.ScaleToAsync(1.0, 120, Easing.CubicIn);
            ShuffleIcon.Rotation = 0.0;
        }
        finally
        {
            _isShufflingAnimation = false;
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

using System;
using Microsoft.Maui.Controls;
#if ANDROID
using Android.Views.Animations;
#endif

namespace MusicPlayer.Features.Player.Animations;

/// <summary>
/// Выполняет анимации шторок и модальных окон через нативный Android ViewPropertyAnimator (RenderThread),
/// полностью разгружая C# UI-поток во время анимации.
/// </summary>
internal static class NativeSheetAnimator
{
    public static void AnimateSlideFade(
        VisualElement element,
        double fromTranslationYDp,
        double toTranslationYDp,
        double fromOpacity,
        double toOpacity,
        uint durationMs,
        bool isOpening,
        bool useHardwareLayer = true,
        Action? onCompleted = null)
    {
#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.Animate()?.Cancel();

            float density = nativeView.Resources?.DisplayMetrics?.Density ?? 1f;
            nativeView.TranslationY = (float)(fromTranslationYDp * density);
            nativeView.Alpha = (float)fromOpacity;

            IInterpolator interpolator = isOpening
                ? new DecelerateInterpolator(1.8f)
                : new AccelerateInterpolator(1.4f);

            var animator = nativeView.Animate()
                ?.TranslationY((float)(toTranslationYDp * density))
                .Alpha((float)toOpacity)
                .SetDuration(durationMs)
                .SetInterpolator(interpolator);

            if (animator is not null)
            {
                if (useHardwareLayer)
                {
                    animator.WithLayer();
                }

                animator.WithEndAction(new Java.Lang.Runnable(() =>
                {
                    element.TranslationY = toTranslationYDp;
                    element.Opacity = toOpacity;
                    onCompleted?.Invoke();
                }));
                animator.Start();
                return;
            }
        }
#endif
        element.TranslationY = toTranslationYDp;
        element.Opacity = toOpacity;
        onCompleted?.Invoke();
    }

    public static void Cancel(VisualElement element)
    {
#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.Animate()?.Cancel();
        }
#endif
    }

    public static void PostAfterLayout(VisualElement element, Action action)
    {
#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.Post(action);
            return;
        }
#endif
        element.Dispatcher.Dispatch(action);
    }
}

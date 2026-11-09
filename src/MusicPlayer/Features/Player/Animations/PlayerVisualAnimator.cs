namespace MusicPlayer.Features.Player.Animations;

using System;
using System.Threading.Tasks;
using Microsoft.Maui;
using Microsoft.Maui.Controls;



public static class PlayerVisualAnimator
{
    private const string ExpandAnimationName = "PlayerExpandTransition";
    private const string CollapseAnimationName = "PlayerCollapseTransition";
    private const string SnapBackAnimationName = "PlayerSnapBackTransition";
    private const string MiniPlayerAnimationName = "MiniPlayerTransition";

    private const uint FrameRateHz = 16; 
    private const uint ExpandDurationMs = 270;
    private const uint CollapseDurationMs = 240;
    private const uint SnapBackDurationMs = 180;
    private const uint MiniPlayerOutDurationMs = 110;
    private const uint MiniPlayerInDurationMs = 200;

    public static void PrepareForExpand(
        VisualElement rootContainer,
        VisualElement header,
        VisualElement cover,
        VisualElement trackInfo,
        VisualElement controls,
        double viewportHeight)
    {
        double startOffsetY = ResolveVerticalOffset(viewportHeight) * 0.72;

        rootContainer.AbortAnimation(ExpandAnimationName);
        rootContainer.AbortAnimation(CollapseAnimationName);

        rootContainer.TranslationY = startOffsetY;
        rootContainer.Scale = 0.92;
        rootContainer.Opacity = 0.0;

        header.Opacity = 0.0;
        header.TranslationY = -12;

        cover.Scale = 0.62;
        cover.TranslationY = 36;

        trackInfo.Opacity = 0.0;
        trackInfo.TranslationY = 20;

        controls.Opacity = 0.0;
        controls.TranslationY = 28;
    }

    public static Task AnimateExpandAsync(
        VisualElement rootContainer,
        VisualElement header,
        VisualElement cover,
        VisualElement trackInfo,
        VisualElement controls)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        double startTranslationY = rootContainer.TranslationY;
        double startScale = rootContainer.Scale;
        double startCoverScale = cover.Scale;
        double startCoverY = cover.TranslationY;

        var composite = new Animation
        {
            { 0.0, 1.0, new Animation(v => rootContainer.TranslationY = v, startTranslationY, 0.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => rootContainer.Scale = v, startScale, 1.0, Easing.CubicOut) },
            { 0.0, 0.55, new Animation(v => rootContainer.Opacity = v, 0.0, 1.0, Easing.CubicOut) },

            { 0.0, 1.0, new Animation(v => cover.Scale = v, startCoverScale, 1.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => cover.TranslationY = v, startCoverY, 0.0, Easing.CubicOut) },

            { 0.20, 1.0, new Animation(v => header.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
            { 0.20, 1.0, new Animation(v => header.TranslationY = v, -12.0, 0.0, Easing.CubicOut) },

            { 0.15, 1.0, new Animation(v => trackInfo.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
            { 0.15, 1.0, new Animation(v => trackInfo.TranslationY = v, 20.0, 0.0, Easing.CubicOut) },

            { 0.22, 1.0, new Animation(v => controls.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
            { 0.22, 1.0, new Animation(v => controls.TranslationY = v, 28.0, 0.0, Easing.CubicOut) }
        };

        composite.Commit(
            owner: rootContainer,
            name: ExpandAnimationName,
            rate: FrameRateHz,
            length: ExpandDurationMs,
            easing: Easing.Linear,
            finished: (_, _) => tcs.TrySetResult(true));

        return tcs.Task;
    }

    public static Task AnimateCollapseAsync(
        VisualElement rootContainer,
        VisualElement header,
        VisualElement cover,
        VisualElement trackInfo,
        VisualElement controls,
        double viewportHeight)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        rootContainer.AbortAnimation(ExpandAnimationName);
        rootContainer.AbortAnimation(CollapseAnimationName);
        rootContainer.AbortAnimation(SnapBackAnimationName);

        double targetOffsetY = ResolveVerticalOffset(viewportHeight) * 0.78;
        double startTranslationY = rootContainer.TranslationY;
        double startScale = rootContainer.Scale;
        double startOpacity = rootContainer.Opacity;
        double startCoverScale = cover.Scale;

        var composite = new Animation
        {
            { 0.0, 0.45, new Animation(v => header.Opacity = v, header.Opacity, 0.0, Easing.CubicIn) },
            { 0.0, 0.50, new Animation(v => controls.Opacity = v, controls.Opacity, 0.0, Easing.CubicIn) },
            { 0.0, 0.55, new Animation(v => trackInfo.Opacity = v, trackInfo.Opacity, 0.0, Easing.CubicIn) },

            { 0.0, 1.0, new Animation(v => cover.Scale = v, startCoverScale, 0.42, Easing.CubicInOut) },
            { 0.0, 1.0, new Animation(v => rootContainer.TranslationY = v, startTranslationY, targetOffsetY, Easing.CubicInOut) },
            { 0.0, 1.0, new Animation(v => rootContainer.Scale = v, startScale, 0.88, Easing.CubicInOut) },
            { 0.25, 1.0, new Animation(v => rootContainer.Opacity = v, startOpacity, 0.0, Easing.CubicIn) }
        };

        composite.Commit(
            owner: rootContainer,
            name: CollapseAnimationName,
            rate: FrameRateHz,
            length: CollapseDurationMs,
            easing: Easing.Linear,
            finished: (_, _) => tcs.TrySetResult(true));

        return tcs.Task;
    }

    public static Task AnimateSnapBackAsync(
        VisualElement rootContainer,
        VisualElement cover)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        rootContainer.AbortAnimation(SnapBackAnimationName);

        var composite = new Animation
        {
            { 0.0, 1.0, new Animation(v => rootContainer.TranslationY = v, rootContainer.TranslationY, 0.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => rootContainer.Scale = v, rootContainer.Scale, 1.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => rootContainer.Opacity = v, rootContainer.Opacity, 1.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => cover.Scale = v, cover.Scale, 1.0, Easing.CubicOut) }
        };

        composite.Commit(
            owner: rootContainer,
            name: SnapBackAnimationName,
            rate: FrameRateHz,
            length: SnapBackDurationMs,
            easing: Easing.Linear,
            finished: (_, _) => tcs.TrySetResult(true));

        return tcs.Task;
    }

    public static Task AnimateMiniPlayerExpandOutAsync(VisualElement miniPlayer)
    {
        if (!miniPlayer.IsVisible)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        miniPlayer.AbortAnimation(MiniPlayerAnimationName);

        var animation = new Animation
        {
            { 0.0, 1.0, new Animation(v => miniPlayer.TranslationY = v, miniPlayer.TranslationY, -16.0, Easing.CubicIn) },
            { 0.0, 1.0, new Animation(v => miniPlayer.Scale = v, miniPlayer.Scale, 0.96, Easing.CubicIn) },
            { 0.0, 1.0, new Animation(v => miniPlayer.Opacity = v, miniPlayer.Opacity, 0.0, Easing.CubicIn) }
        };

        animation.Commit(
            owner: miniPlayer,
            name: MiniPlayerAnimationName,
            rate: FrameRateHz,
            length: MiniPlayerOutDurationMs,
            easing: Easing.Linear,
            finished: (_, _) => tcs.TrySetResult(true));

        return tcs.Task;
    }

    public static Task AnimateMiniPlayerCollapseInAsync(VisualElement miniPlayer)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        miniPlayer.AbortAnimation(MiniPlayerAnimationName);

        miniPlayer.TranslationY = 24.0;
        miniPlayer.Scale = 0.94;
        miniPlayer.Opacity = 0.0;

        var animation = new Animation
        {
            { 0.0, 1.0, new Animation(v => miniPlayer.TranslationY = v, 24.0, 0.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => miniPlayer.Scale = v, 0.94, 1.0, Easing.CubicOut) },
            { 0.0, 1.0, new Animation(v => miniPlayer.Opacity = v, 0.0, 1.0, Easing.CubicOut) }
        };

        animation.Commit(
            owner: miniPlayer,
            name: MiniPlayerAnimationName,
            rate: FrameRateHz,
            length: MiniPlayerInDurationMs,
            easing: Easing.Linear,
            finished: (_, _) => tcs.TrySetResult(true));

        return tcs.Task;
    }

    private static double ResolveVerticalOffset(double viewportHeight)
    {
        return viewportHeight > 100 ? viewportHeight : 800.0;
    }
}
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Tracks.Components;

public partial class SelectPlaylistForTrackSheet : ContentView
{
    private const string ModalAnimationName = "SelectPlaylistForTrackSheetTransition";
    private const uint FrameRateMs = 8;
    private const uint OpenDurationMs = 220;
    private const uint CloseDurationMs = 160;
    private const double HiddenTranslationY = 28.0;

    private int _animationGeneration;

    public static readonly BindableProperty IsOpenProperty = BindableProperty.Create(
        nameof(IsOpen),
        typeof(bool),
        typeof(SelectPlaylistForTrackSheet),
        defaultValue: false,
        propertyChanged: OnIsOpenPropertyChanged);

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public SelectPlaylistForTrackSheet()
    {
        InitializeComponent();
    }

    private static void OnIsOpenPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SelectPlaylistForTrackSheet sheet && newValue is bool isOpen)
        {
            sheet.AnimateModalState(isOpen);
        }
    }

    private void AnimateModalState(bool isOpen)
    {
        int generation = ++_animationGeneration;
        this.AbortAnimation(ModalAnimationName);

        if (isOpen)
        {
            BackdropScrim.Opacity = 0.0;
            ModalCard.Opacity = 0.0;
            ModalCard.TranslationY = HiddenTranslationY;

            IsVisible = true;
            InputTransparent = false;

            // Откладываем старт анимации на один цикл рендеринга, чтобы Measure/Arrange
            // CollectionView и карточки завершились до первого кадра анимации.
            Dispatcher.Dispatch(() =>
            {
                if (!IsOpen || generation != _animationGeneration)
                    return;

                AndroidRenderLayerHelper.SetHardwareLayer(ModalCard, true);
                AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, true);

                var openAnimation = new Animation
                {
                    { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
                    { 0.0, 0.85, new Animation(v => ModalCard.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
                    { 0.0, 1.0, new Animation(v => ModalCard.TranslationY = v, HiddenTranslationY, 0.0, Easing.CubicOut) }
                };

                openAnimation.Commit(
                    owner: this,
                    name: ModalAnimationName,
                    rate: FrameRateMs,
                    length: OpenDurationMs,
                    easing: Easing.Linear,
                    finished: (_, _) =>
                    {
                        AndroidRenderLayerHelper.SetHardwareLayer(ModalCard, false);
                        AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, false);
                    });
            });
        }
        else
        {
            InputTransparent = true;

            double startScrimOpacity = BackdropScrim.Opacity;
            double startCardOpacity = ModalCard.Opacity;
            double startTranslationY = ModalCard.TranslationY;

            AndroidRenderLayerHelper.SetHardwareLayer(ModalCard, true);
            AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, true);

            var closeAnimation = new Animation
            {
                { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, startScrimOpacity, 0.0, Easing.CubicIn) },
                { 0.0, 1.0, new Animation(v => ModalCard.Opacity = v, startCardOpacity, 0.0, Easing.CubicIn) },
                { 0.0, 1.0, new Animation(v => ModalCard.TranslationY = v, startTranslationY, HiddenTranslationY, Easing.CubicIn) }
            };

            closeAnimation.Commit(
                owner: this,
                name: ModalAnimationName,
                rate: FrameRateMs,
                length: CloseDurationMs,
                easing: Easing.Linear,
                finished: (_, _) =>
                {
                    AndroidRenderLayerHelper.SetHardwareLayer(ModalCard, false);
                    AndroidRenderLayerHelper.SetHardwareLayer(BackdropScrim, false);

                    if (!IsOpen && generation == _animationGeneration)
                    {
                        IsVisible = false;
                    }
                });
        }
    }
}
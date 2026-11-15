using Microsoft.Maui;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Tracks.Components;

public partial class SelectPlaylistForTrackSheet : ContentView
{
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
        NativeSheetAnimator.Cancel(ModalCard);
        NativeSheetAnimator.Cancel(BackdropScrim);

        if (isOpen)
        {
            BackdropScrim.Opacity = 0.0;
            ModalCard.Opacity = 0.0;
            ModalCard.TranslationY = HiddenTranslationY;

            IsVisible = true;
            InputTransparent = false;

            NativeSheetAnimator.PostAfterLayout(ModalCard, () =>
            {
                if (!IsOpen || generation != _animationGeneration)
                    return;

                NativeSheetAnimator.AnimateSlideFade(
                    BackdropScrim,
                    0.0, 0.0,
                    0.0, 1.0,
                    OpenDurationMs,
                    isOpening: true,
                    useHardwareLayer: false);

                NativeSheetAnimator.AnimateSlideFade(
                    ModalCard,
                    HiddenTranslationY, 0.0,
                    0.0, 1.0,
                    OpenDurationMs,
                    isOpening: true);
            });
        }
        else
        {
            InputTransparent = true;

            double startScrimOpacity = BackdropScrim.Opacity;
            double startCardOpacity = ModalCard.Opacity;
            double startTranslationY = ModalCard.TranslationY;

            NativeSheetAnimator.AnimateSlideFade(
                BackdropScrim,
                0.0, 0.0,
                startScrimOpacity, 0.0,
                CloseDurationMs,
                isOpening: false,
                useHardwareLayer: false);

            NativeSheetAnimator.AnimateSlideFade(
                ModalCard,
                startTranslationY, HiddenTranslationY,
                startCardOpacity, 0.0,
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
}
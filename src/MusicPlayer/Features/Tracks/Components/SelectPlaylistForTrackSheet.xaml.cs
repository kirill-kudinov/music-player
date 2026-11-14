using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace MusicPlayer.Features.Tracks.Components;

public partial class SelectPlaylistForTrackSheet : ContentView
{
    private const string ModalAnimationName = "SelectPlaylistForTrackSheetTransition";
    private const uint FrameRateHz = 16;
    private const uint OpenDurationMs = 200;
    private const uint CloseDurationMs = 150;

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
        this.AbortAnimation(ModalAnimationName);

        if (isOpen)
        {
            IsVisible = true;
            InputTransparent = false;

            BackdropScrim.Opacity = 0.0;
            ModalCard.Opacity = 0.0;
            ModalCard.Scale = 0.95;

            var openAnimation = new Animation
            {
                { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
                { 0.0, 1.0, new Animation(v => ModalCard.Opacity = v, 0.0, 1.0, Easing.CubicOut) },
                { 0.0, 1.0, new Animation(v => ModalCard.Scale = v, 0.95, 1.0, Easing.CubicOut) }
            };

            openAnimation.Commit(
                owner: this,
                name: ModalAnimationName,
                rate: FrameRateHz,
                length: OpenDurationMs,
                easing: Easing.Linear);
        }
        else
        {
            InputTransparent = true;

            double startScrimOpacity = BackdropScrim.Opacity;
            double startCardOpacity = ModalCard.Opacity;
            double startCardScale = ModalCard.Scale;

            var closeAnimation = new Animation
            {
                { 0.0, 1.0, new Animation(v => BackdropScrim.Opacity = v, startScrimOpacity, 0.0, Easing.CubicIn) },
                { 0.0, 1.0, new Animation(v => ModalCard.Opacity = v, startCardOpacity, 0.0, Easing.CubicIn) },
                { 0.0, 1.0, new Animation(v => ModalCard.Scale = v, startCardScale, 0.95, Easing.CubicIn) }
            };

            closeAnimation.Commit(
                owner: this,
                name: ModalAnimationName,
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
}
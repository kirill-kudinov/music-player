using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Player;

public sealed class PlayerAppearanceController(ITrackCoverExtractor coverExtractor) : IDisposable
{
    public static readonly Color DefaultWineColor = Color.FromArgb("#7C1919");

    private CancellationTokenSource? _colorExtractionCts;

    public (ImageSource? Cover, Color FallbackColor) UpdateForTrack(
        Uri? contentUri,
        Action<Color> onDominantColorResolved)
    {
        _colorExtractionCts?.Cancel();
        _colorExtractionCts?.Dispose();
        _colorExtractionCts = null;

        if (contentUri is null)
            return (null, DefaultWineColor);

        ImageSource? cover = coverExtractor.ExtractCover(contentUri);

        var cts = new CancellationTokenSource();
        _colorExtractionCts = cts;
        CancellationToken token = cts.Token;

        _ = Task.Run(() =>
        {
            Color? extracted = coverExtractor.ExtractDominantColor(contentUri);
            if (token.IsCancellationRequested)
                return;

            Color targetColor = extracted ?? DefaultWineColor;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!token.IsCancellationRequested)
                {
                    onDominantColorResolved(targetColor);
                }
            });
        }, token);

        return (cover, DefaultWineColor);
    }

    public void Dispose()
    {
        _colorExtractionCts?.Cancel();
        _colorExtractionCts?.Dispose();
        _colorExtractionCts = null;
    }
}

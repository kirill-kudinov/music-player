using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace MusicPlayer.Playback;

public interface ITrackCoverExtractor
{
    ImageSource? ExtractCover(Uri? contentUri);
    Color? ExtractDominantColor(Uri? contentUri);
    string? GetCachedCoverFilePath(Uri? contentUri);
}
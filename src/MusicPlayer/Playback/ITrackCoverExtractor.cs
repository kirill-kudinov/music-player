using System;
using Microsoft.Maui.Controls;

namespace MusicPlayer.Playback;

public interface ITrackCoverExtractor
{
    ImageSource? ExtractCover(Uri? contentUri);
}
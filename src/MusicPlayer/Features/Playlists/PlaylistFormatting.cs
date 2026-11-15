using System;

namespace MusicPlayer.Features.Playlists;

internal static class PlaylistFormatting
{
    public static string FormatTrackCount(int count)
    {
        int abs = Math.Abs(count);
        int mod100 = abs % 100;
        if (mod100 is >= 11 and <= 19)
            return $"{count} треков";

        return (abs % 10) switch
        {
            1 => $"{count} трек",
            2 or 3 or 4 => $"{count} трека",
            _ => $"{count} треков"
        };
    }
}

namespace MusicPlayer.Core.AudioTracks;

public static class MimeTypeParser
{
    public static AudioFormat Parse(string? mimeType) =>
        mimeType?.Trim().ToLowerInvariant() switch
        {
            "audio/mp4" or "audio/aac" => AudioFormat.Aac,
            "audio/flac" => AudioFormat.Flac,
            "audio/ogg" => AudioFormat.Ogg,
            "audio/opus" => AudioFormat.Opus,
            _ => AudioFormat.Unknown
        };
}
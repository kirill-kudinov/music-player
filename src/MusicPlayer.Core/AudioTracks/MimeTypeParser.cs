namespace MusicPlayer.Core.AudioTracks;

public static class MimeTypeParser
{
    public static AudioFormat Parse(string? mimeType) =>
        mimeType?.Trim().ToLowerInvariant() switch
    {
        "audio/mpeg" => AudioFormat.Mp3,
        "audio/mp4" or "audio/aac" => AudioFormat.Aac,
        "audio/flac" => AudioFormat.Flac,
        "audio/ogg" => AudioFormat.OggVorbis,
        "audio/opus" => AudioFormat.Opus,
        "audio/x-wav" or "audio/wav" => AudioFormat.Wav,
        _ => AudioFormat.Unknown
    };
}
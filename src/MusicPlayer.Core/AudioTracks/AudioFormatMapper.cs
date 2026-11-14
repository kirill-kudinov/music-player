namespace MusicPlayer.Core.AudioTracks;

public static class AudioFormatMapper
{
    public static AudioFormat ToEntity(string? audioFormat) =>
        audioFormat?.Trim().ToLowerInvariant() switch
        {
            "audio/mp4" or "audio/aac" => AudioFormat.Aac,
            "audio/flac" => AudioFormat.Flac,
            "audio/ogg" => AudioFormat.Ogg,
            "audio/opus" => AudioFormat.Opus,
            _ => AudioFormat.Unknown
        };

    public static string ToString(AudioFormat? audioFormat) => audioFormat switch
    {
        AudioFormat.Aac => "audio/aac",
        AudioFormat.Flac => "audio/flac",
        AudioFormat.Ogg => "audio/ogg",
        AudioFormat.Opus => "audio/opus",
        _ => "audio/unknown"
    };
}
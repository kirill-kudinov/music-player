namespace MusicPlayer.Core.AudioTracks;

public static class FileTypeMapper
{
    public static AudioFormat ToEntity(string? fileType) =>
        fileType?.Trim().ToLowerInvariant() switch
        {
            "audio/mp4" or "audio/aac" => AudioFormat.Aac,
            "audio/flac" => AudioFormat.Flac,
            "audio/ogg" => AudioFormat.Ogg,
            "audio/opus" => AudioFormat.Opus,
            _ => AudioFormat.Unknown
        };
    
    public static string ToString(AudioFormat )
}
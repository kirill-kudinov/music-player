using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.AudioTracks;

public static class AudioTrackErrors
{

    public static readonly Error NegativeOrZeroDuration = new(
        "AudioTrack.NegativeOrZeroDuration", "Длительность трека не может быть отрицательной или ровняться нулю."
    );

    public static readonly Error EmptyTitle = new(
        "AudioTrack.EmptyTitle", "Название трека не может быть пустым."
    );
    
    public static readonly Error EmptyArtist = new(
        "AudioTrack.EmptyArtist", "Имя исполнителя не может быть пустым."
    );
    
    public static readonly Error EmptyContentUri = new(
        "AudioTrack.EmptyContentUri", "URI аудиоресурса не может быть пустым."
    );
}
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.AudioTracks;

public static class AudioTrackErrors
{
    public static readonly Error EmptyFilePath = new(
        "AudioTrack.EmptyFilePath", "Путь к файлу не может быть пустым."
    );


    public static readonly Error NegativeOrZeroDuration = new(
        "AudioTrack.NegativeOrZeroDuration", "Длительность трека не может быть отрицательной или ровняться нулю."
    );

    public static readonly Error InvalidRenaming = new(
        "AudioTrack.InvalidRenaming", "Название не может быть пустым."
    );

    public static readonly Error InvalidPathCharacters = new(
        "AudioTrack.InvalidPathCharacters ", $"Недопустимые символы в пути."
    );
}
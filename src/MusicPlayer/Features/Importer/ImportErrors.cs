using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public static class ImportErrors
{
    public static readonly Error PathIsEmpty = new(
        "TrackStorage.PathIsEmpty", "Путь до файла не может быть пустым"
    );

    public static readonly Error AudioFileNotFound = new(
        "TrackStorageErrors", "Файл должен быть аудио"
    );
}
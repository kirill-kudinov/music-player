using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Playlists.Data;

public static class PlaylistStorageErrors
{
    public static readonly Error DatabaseOperationFailed = new(
        "PlaylistStorage.DatabaseOperationFailed",
        "Не удалось выполнить операцию с локальной базой данных.");

    public static readonly Error CoverCopyFailed = new(
        "PlaylistStorage.CoverCopyFailed",
        "Не удалось сохранить файл обложки во внутреннее хранилище.");

    public static readonly Error InvalidCoverFile = new(
        "PlaylistStorage.InvalidCoverFile",
        "Выбранный файл обложки недоступен или повреждён.");
}

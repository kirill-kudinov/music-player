using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.Playlists;

public static class PlaylistErrors
{
    public static readonly Error EmptyId = new(
        "Playlist.EmptyId",
        "Идентификатор плейлиста не может быть пустым.");

    public static readonly Error EmptyTitle = new(
        "Playlist.EmptyTitle",
        "Название плейлиста не может быть пустым.");

    public static readonly Error TitleTooLong = new(
        "Playlist.TitleTooLong",
        "Название плейлиста не может превышать 100 символов.");

    public static readonly Error EmptyCoverPath = new(
        "Playlist.EmptyCoverPath",
        "Путь к файлу обложки не может быть пустым.");

    public static readonly Error SystemPlaylistImmutable = new(
        "Playlist.SystemPlaylistImmutable",
        "Нельзя изменить название или обложку системного плейлиста «Избранное».");

    public static readonly Error TrackAlreadyInPlaylist = new(
        "Playlist.TrackAlreadyInPlaylist",
        "Трек уже добавлен в этот плейлист.");

    public static readonly Error TrackNotInPlaylist = new(
        "Playlist.TrackNotInPlaylist",
        "Трек не найден в этом плейлисте.");

    public static readonly Error CannotDeleteSystemPlaylist = new(
        "Playlist.CannotDeleteSystemPlaylist",
        "Нельзя удалить системный плейлист «Избранное».");

    public static readonly Error NotFound = new(
        "Playlist.NotFound",
        "Плейлист не найден.");
}

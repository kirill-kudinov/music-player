using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.Playlist;

public static class PlaylistErrors
{
    public static readonly Error EmptyName = new(
        "PlaylistErrors.EmptyName",
        "Имя плейлиста не может быть пустым"
    );
}
using SQLite;

namespace MusicPlayer.Infrastructure;

public sealed class PlaylistItem
{
    [PrimaryKey] [AutoIncrement] public int Id { get; set; }
    public string Name { get; set; }
}
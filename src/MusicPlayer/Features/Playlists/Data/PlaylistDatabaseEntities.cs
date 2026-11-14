using SQLite;

namespace MusicPlayer.Features.Playlists.Data;

[Table("playlists")]
public sealed class PlaylistEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [NotNull]
    public string Title { get; set; } = string.Empty;

    public string? CoverPath { get; set; }

    [Indexed]
    public bool IsSystem { get; set; }

    public long CreatedAtUtcTicks { get; set; }
}

[Table("track_cache")]
public sealed class TrackCacheEntity
{
    [PrimaryKey]
    public string ContentUri { get; set; } = string.Empty;

    [NotNull]
    public string Title { get; set; } = string.Empty;

    [NotNull]
    public string Artist { get; set; } = string.Empty;

    public int Format { get; set; }

    public long DurationTicks { get; set; }

    public bool IsFavorite { get; set; }
}

[Table("playlist_tracks")]
public sealed class PlaylistTrackEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "IX_PlaylistTrack_Unique", Order = 1, Unique = true)]
    public string PlaylistId { get; set; } = string.Empty;

    [Indexed(Name = "IX_PlaylistTrack_Unique", Order = 2, Unique = true)]
    public string TrackContentUri { get; set; } = string.Empty;

    [Indexed]
    public int SortOrder { get; set; }
}

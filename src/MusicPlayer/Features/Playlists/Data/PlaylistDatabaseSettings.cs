using System.IO;
using Microsoft.Maui.Storage;
using SQLite;

namespace MusicPlayer.Features.Playlists.Data;

public sealed record PlaylistDatabaseSettings
{
    public string DatabasePath { get; } = Path.Combine(FileSystem.Current.AppDataDirectory, "fluentmusic.db3");

    public string CoversDirectory { get; } = Path.Combine(FileSystem.Current.AppDataDirectory, "playlist_covers");

    public SQLiteOpenFlags OpenFlags { get; } =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;
}

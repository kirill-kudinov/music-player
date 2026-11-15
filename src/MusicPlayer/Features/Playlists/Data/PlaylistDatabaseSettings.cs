using System.IO;
using Microsoft.Maui.Storage;
using SQLite;

namespace MusicPlayer.Features.Playlists.Data;

public sealed record PlaylistDatabaseSettings
{
    public string DatabasePath { get; }

    public string CoversDirectory { get; }

    public SQLiteOpenFlags OpenFlags { get; } =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;

    public PlaylistDatabaseSettings()
        : this(
            Path.Combine(FileSystem.Current.AppDataDirectory, "fluentmusic.db3"),
            Path.Combine(FileSystem.Current.AppDataDirectory, "playlist_covers"))
    {
    }

    public PlaylistDatabaseSettings(string databasePath, string coversDirectory)
    {
        DatabasePath = databasePath;
        CoversDirectory = coversDirectory;
    }
}

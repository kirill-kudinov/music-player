using System.IO;
using Microsoft.Maui.Storage;
using SQLite;

namespace MusicPlayer.Infrastructure;

public static class DatabaseConfiguration
{
    private const string DatabaseFilename = "MusicPlayerSQLite.db3";

    public const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;

    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
}
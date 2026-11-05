using Microsoft.Maui.Storage;

namespace MusicPlayer.Features.Importer;

public sealed record TrackStorageSettings
{
    public string BaseDirectory { get; } = FileSystem.Current.AppDataDirectory + "/audio/";
}
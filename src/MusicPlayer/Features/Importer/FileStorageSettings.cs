using Microsoft.Maui.Storage;

namespace MusicPlayer.Features.Importer;

public sealed record FileStorageSettings
{
    public string BaseDirectory { get; } = FileSystem.Current.AppDataDirectory + "/audio/";
}
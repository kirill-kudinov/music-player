using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed class TrackStorage(TrackStorageSettings trackStorageSettings)
{
    public async Task<Result<string>> AddTrackAsync(string relativePath, FileStream content)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Result.Fail(ImportErrors.PathIsEmpty);

        var fullPath = Path.Combine(trackStorageSettings.BaseDirectory, relativePath);

        var directoryPath = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        const int bufferSize = 81920;

        await using var destinationStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            useAsync: true
        );

        await content.CopyToAsync(destinationStream, bufferSize);

        return Result.Ok(fullPath);
    }
    
    public List<AudioTrack> GetAllTracks()
    {
        if (!Directory.Exists(trackStorageSettings.BaseDirectory)) return [];

        var availableFiles = Directory.GetFiles(trackStorageSettings.BaseDirectory);

        if (availableFiles.Length == 0) return [];

        List<AudioTrack> audioTracks = [];

        foreach (var availableFile in availableFiles)
        {
            var createResult = AudioTrack.Create(availableFile);

            if (!createResult.TryGetOk(out var audioTrack)) continue;
            
            audioTracks.Add(audioTrack);
        }

        return audioTracks;
    }
}
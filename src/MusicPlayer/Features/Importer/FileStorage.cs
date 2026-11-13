using System.IO;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed class FileStorage(FileStorageSettings fileStorageSettings)
{
    public async Task<Result<string>> AddAsync(string relativePath, FileStream content)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Result.Fail(ImportErrors.PathIsEmpty);

        var fullPath = Path.Combine(fileStorageSettings.BaseDirectory, relativePath);

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
}
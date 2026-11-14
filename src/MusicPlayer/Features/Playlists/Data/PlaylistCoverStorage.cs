using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Playlists.Data;

public sealed class PlaylistCoverStorage(PlaylistDatabaseSettings settings)
{
    private const int CopyBufferSize = 81920;

    public async Task<Result<string>> SaveCoverAsync(
        FileResult photo,
        CancellationToken cancellationToken = default)
    {
        if (photo is null)
            return Result.Fail(PlaylistStorageErrors.InvalidCoverFile);

        try
        {
            Directory.CreateDirectory(settings.CoversDirectory);

            string extension = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".jpg";
            }

            string fileName = $"{Guid.NewGuid():N}{extension}";
            string destinationPath = Path.GetFullPath(Path.Combine(settings.CoversDirectory, fileName));

            await using Stream sourceStream = await photo.OpenReadAsync();
            await using FileStream destinationStream = new(
                destinationPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                useAsync: true);

            await sourceStream.CopyToAsync(destinationStream, CopyBufferSize, cancellationToken);
            await destinationStream.FlushAsync(cancellationToken);

            return Result.Ok(destinationPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaylistCoverStorage] Failed to copy cover: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.CoverCopyFailed);
        }
    }

    public void DeleteCoverIfExists(string? coverPath)
    {
        if (string.IsNullOrWhiteSpace(coverPath))
            return;

        try
        {
            string normalizedCoversDir = Path.GetFullPath(settings.CoversDirectory);
            string normalizedTarget = Path.GetFullPath(coverPath);

            if (!normalizedTarget.StartsWith(normalizedCoversDir, StringComparison.OrdinalIgnoreCase))
                return;

            if (File.Exists(normalizedTarget))
            {
                File.Delete(normalizedTarget);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaylistCoverStorage] Failed to delete obsolete cover '{coverPath}': {ex.Message}");
        }
    }
}

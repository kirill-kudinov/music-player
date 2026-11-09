using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed class TrackStorage(TrackStorageSettings trackStorageSettings)
{
    private readonly HashSet<string> _excludedPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Событие вызывается сразу после успешного сохранения нового трека в хранилище плеера.
    /// </summary>
    public event EventHandler<string>? TrackAdded;

    /// <summary>
    /// Событие вызывается при удалении трека из библиотеки плеера.
    /// </summary>
    public event EventHandler<string>? TrackRemoved;

    public async Task<Result<string>> AddTrackAsync(string relativePath, Stream content)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Result.Fail(ImportErrors.PathIsEmpty);

        ArgumentNullException.ThrowIfNull(content);

        string fullPath = Path.GetFullPath(Path.Combine(trackStorageSettings.BaseDirectory, relativePath));
        string? directoryPath = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        const int bufferSize = 81920;

        await using (var destinationStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize,
            useAsync: true))
        {
            await content.CopyToAsync(destinationStream, bufferSize);
            await destinationStream.FlushAsync();
        }

        _excludedPaths.Remove(fullPath);
        TrackAdded?.Invoke(this, fullPath);

        return Result.Ok(fullPath);
    }

    public Task<Result<string>> AddTrackAsync(string relativePath, FileStream content) =>
        AddTrackAsync(relativePath, (Stream)content);

    /// <summary>
    /// Удаляет трек из библиотеки плеера (внутреннего кэша приложения),
    /// не затрагивая исходный файл в памяти телефона пользователя.
    /// </summary>
    public void RemoveTrackFromPlayer(Uri? contentUri)
    {
        if (contentUri is null)
            return;

        string rawPath = contentUri.IsFile ? contentUri.LocalPath : contentUri.OriginalString;
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        string normalizedPath = Path.GetFullPath(rawPath);
        _excludedPaths.Add(normalizedPath);

        try
        {
            string baseDirFull = Path.GetFullPath(trackStorageSettings.BaseDirectory);
            if (normalizedPath.StartsWith(baseDirFull, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(normalizedPath))
            {
                File.Delete(normalizedPath);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackStorage] Не удалось удалить внутреннюю копию файла: {ex.Message}");
        }

        TrackRemoved?.Invoke(this, normalizedPath);
    }

    public bool IsExcluded(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        string normalizedPath = Path.GetFullPath(filePath);
        return _excludedPaths.Contains(normalizedPath);
    }

    public List<AudioTrack> GetAllTracks()
    {
        if (!Directory.Exists(trackStorageSettings.BaseDirectory))
            return [];

        string[] availableFiles = Directory.GetFiles(trackStorageSettings.BaseDirectory);
        if (availableFiles.Length == 0)
            return [];

        List<AudioTrack> audioTracks = [];

        foreach (string availableFile in availableFiles)
        {
            if (IsExcluded(availableFile))
                continue;

            var createResult = AudioTrack.Create(availableFile);
            if (!createResult.TryGetOk(out var audioTrack))
                continue;

            audioTracks.Add(audioTrack);
        }

        return audioTracks;
    }
}
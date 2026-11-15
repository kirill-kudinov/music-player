using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed class TrackStorage
{
    private readonly TrackStorageSettings _settings;
    private readonly HashSet<string> _excludedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedUris = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _excludedUrisFilePath = Path.Combine(FileSystem.Current.AppDataDirectory, "excluded_tracks.txt");
    private readonly object _syncLock = new();

    /// <summary>
    /// Событие вызывается сразу после успешного сохранения нового трека в хранилище плеера.
    /// </summary>
    public event EventHandler<string>? TrackAdded;

    /// <summary>
    /// Событие вызывается при удалении трека из библиотеки плеера.
    /// </summary>
    public event EventHandler<string>? TrackRemoved;

    public TrackStorage(TrackStorageSettings trackStorageSettings)
    {
        _settings = trackStorageSettings;
        LoadExcludedUris();
    }

    public async Task<Result<string>> AddTrackAsync(string relativePath, Stream content)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Result.Fail(ImportErrors.PathIsEmpty);

        ArgumentNullException.ThrowIfNull(content);

        try
        {
            string safeName = Path.GetFileName(relativePath);
            if (string.IsNullOrWhiteSpace(safeName))
                return Result.Fail(ImportErrors.PathIsEmpty);

            string fullPath = Path.GetFullPath(Path.Combine(_settings.BaseDirectory, safeName));
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

            lock (_syncLock)
            {
                _excludedPaths.Remove(fullPath);
            }

            TrackAdded?.Invoke(this, fullPath);

            return Result.Ok(fullPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackStorage] AddTrackAsync failed: {ex.Message}");
            return Result.Fail(ImportErrors.AudioFileNotFound);
        }
    }

    public Task<Result<string>> AddTrackAsync(string relativePath, FileStream content) =>
        AddTrackAsync(relativePath, (Stream)content);

    /// <summary>
    /// Удаляет трек из библиотеки плеера (внутреннего кэша или индекса),
    /// не затрагивая исходный файл в памяти телефона пользователя.
    /// </summary>
    public void RemoveTrackFromPlayer(Uri? contentUri)
    {
        if (contentUri is null)
            return;

        lock (_syncLock)
        {
            if (_excludedUris.Add(contentUri.OriginalString))
            {
                SaveExcludedUris();
            }
        }

        if (contentUri.IsAbsoluteUri && contentUri.IsFile)
        {
            string normalizedPath = Path.GetFullPath(contentUri.LocalPath);
            lock (_syncLock)
            {
                _excludedPaths.Add(normalizedPath);
            }

            try
            {
                string baseDirFull = Path.GetFullPath(_settings.BaseDirectory);
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
            return;
        }

        TrackRemoved?.Invoke(this, contentUri.OriginalString);
    }

    public bool IsExcluded(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        string normalizedPath = Path.GetFullPath(filePath);
        lock (_syncLock)
        {
            return _excludedPaths.Contains(normalizedPath);
        }
    }

    public bool IsExcludedUri(Uri? contentUri)
    {
        if (contentUri is null)
            return false;

        lock (_syncLock)
        {
            return _excludedUris.Contains(contentUri.OriginalString);
        }
    }

    public List<AudioTrack> GetAllTracks()
    {
        if (!Directory.Exists(_settings.BaseDirectory))
            return [];

        string[] availableFiles = Directory.GetFiles(_settings.BaseDirectory);
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

    private void LoadExcludedUris()
    {
        try
        {
            if (!File.Exists(_excludedUrisFilePath))
                return;

            foreach (string line in File.ReadAllLines(_excludedUrisFilePath))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _excludedUris.Add(line.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackStorage] Failed to load excluded URIs: {ex.Message}");
        }
    }

    private void SaveExcludedUris()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_excludedUrisFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllLines(_excludedUrisFilePath, _excludedUris);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackStorage] Failed to save excluded URIs: {ex.Message}");
        }
    }
}
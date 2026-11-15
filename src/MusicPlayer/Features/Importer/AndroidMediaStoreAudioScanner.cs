using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;
using AudioFormat = MusicPlayer.Core.AudioTracks.AudioFormat;
using AudioTrack = MusicPlayer.Core.AudioTracks.AudioTrack;
using Debug = System.Diagnostics.Debug;
using OperationCanceledException = Android.OS.OperationCanceledException;
#if ANDROID
using Android;
using Android.Content;
using Android.Database;
using Android.Media;
using Android.OS;
using Android.Provider;
using Application = Android.App.Application;
#endif

namespace MusicPlayer.Features.Importer;

public sealed class AndroidMediaStoreAudioScanner(TrackStorage trackStorage) : IMediaStoreAudioScanner
{
    private readonly object _cacheLock = new();
    private List<AudioTrack> _cachedTracks = [];
    private HashSet<string> _cachedTrackUris = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<IReadOnlyList<AudioTrack>>? TracksScanned;

    public IReadOnlyList<AudioTrack> CachedTracks
    {
        get
        {
            lock (_cacheLock)
            {
                return [.. _cachedTracks];
            }
        }
    }

    public bool ContainsCachedTrack(Uri? contentUri)
    {
        if (contentUri is null)
            return false;

        lock (_cacheLock)
        {
            return _cachedTrackUris.Contains(contentUri.OriginalString);
        }
    }

    public async Task<bool> HasReadAudioPermissionAsync()
    {
#if ANDROID
        PermissionStatus status = await MainThread.InvokeOnMainThreadAsync(() =>
            Permissions.CheckStatusAsync<ReadAudioPermission>());
        return status == PermissionStatus.Granted;
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public async Task<Result<IReadOnlyList<AudioTrack>>> ScanDeviceAudioAsync(
        IProgress<MediaScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
#if ANDROID
        try
        {
            PermissionStatus status = await MainThread.InvokeOnMainThreadAsync(() =>
                Permissions.RequestAsync<ReadAudioPermission>());

            if (status != PermissionStatus.Granted)
                return Result.Fail(ImportErrors.AudioFileNotFound);

            (List<AudioTrack> scanned, HashSet<string> scannedUris) = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return QueryMediaStoreTracks(progress, cancellationToken);
            }, cancellationToken);

            lock (_cacheLock)
            {
                _cachedTracks = scanned;
                _cachedTrackUris = scannedUris;
            }

            TracksScanned?.Invoke(this, scanned);
            return Result.Ok<IReadOnlyList<AudioTrack>>(scanned);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail(ImportErrors.AudioFileNotFound);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidMediaStoreAudioScanner] ScanDeviceAudioAsync failed: {ex.Message}");
            return Result.Fail(ImportErrors.AudioFileNotFound);
        }
#else
        await Task.CompletedTask;
        return Result.Ok<IReadOnlyList<AudioTrack>>([]);
#endif
    }

#if ANDROID
    private (List<AudioTrack> Tracks, HashSet<string> Uris) QueryMediaStoreTracks(
        IProgress<MediaScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        List<AudioTrack> tracks = [];
        HashSet<string> seenUris = new(StringComparer.OrdinalIgnoreCase);

        ContentResolver? resolver = Application.Context.ContentResolver;
        global::Android.Net.Uri? collectionUri = MediaStore.Audio.Media.ExternalContentUri;

        if (resolver is null || collectionUri is null)
            return (tracks, seenUris);

        string[] projection =
        [
            MediaStore.Audio.Media.InterfaceConsts.Id,
            MediaStore.Audio.Media.InterfaceConsts.Title,
            MediaStore.Audio.Media.InterfaceConsts.DisplayName,
            MediaStore.Audio.Media.InterfaceConsts.Artist,
            MediaStore.Audio.Media.InterfaceConsts.Duration,
            MediaStore.Audio.Media.InterfaceConsts.MimeType,
            MediaStore.Audio.Media.InterfaceConsts.IsRingtone,
            MediaStore.Audio.Media.InterfaceConsts.IsNotification,
            MediaStore.Audio.Media.InterfaceConsts.IsAlarm
        ];

        string sortOrder = $"{MediaStore.Audio.Media.InterfaceConsts.Title} ASC";

        using ICursor? cursor = resolver.Query(collectionUri, projection, null, null, sortOrder);
        if (cursor is null)
            return (tracks, seenUris);

        int idCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Id);
        int titleCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Title);
        int displayNameCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.DisplayName);
        int artistCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Artist);
        int durationCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.Duration);
        int mimeCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.MimeType);
        int ringtoneCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.IsRingtone);
        int notificationCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.IsNotification);
        int alarmCol = cursor.GetColumnIndex(MediaStore.Audio.Media.InterfaceConsts.IsAlarm);

        if (idCol < 0)
            return (tracks, seenUris);

        int total = cursor.Count;
        int processed = 0;

        while (cursor.MoveToNext())
        {
            cancellationToken.ThrowIfCancellationRequested();

            processed++;
            if (processed % 5 == 0 || processed == total)
            {
                progress?.Report(new MediaScanProgress(processed, total));
            }

            // Пропускаем системные рингтоны, уведомления и будильники
            if ((ringtoneCol >= 0 && cursor.GetInt(ringtoneCol) != 0) ||
                (notificationCol >= 0 && cursor.GetInt(notificationCol) != 0) ||
                (alarmCol >= 0 && cursor.GetInt(alarmCol) != 0))
            {
                continue;
            }

            long id = cursor.GetLong(idCol);
            global::Android.Net.Uri? itemAndroidUri = ContentUris.WithAppendedId(collectionUri, id);
            string? uriString = itemAndroidUri?.ToString();

            if (string.IsNullOrWhiteSpace(uriString) ||
                !Uri.TryCreate(uriString, UriKind.Absolute, out Uri? contentUri) ||
                trackStorage.IsExcludedUri(contentUri) ||
                !seenUris.Add(uriString))
            {
                continue;
            }

            string? title = titleCol >= 0 ? cursor.GetString(titleCol) : null;
            if (string.IsNullOrWhiteSpace(title) && displayNameCol >= 0)
            {
                string? displayName = cursor.GetString(displayNameCol);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    title = System.IO.Path.GetFileNameWithoutExtension(displayName);
                }
            }

            string? rawArtist = artistCol >= 0 ? cursor.GetString(artistCol) : null;
            string? artist = string.Equals(rawArtist, "<unknown>", StringComparison.OrdinalIgnoreCase)
                ? null
                : rawArtist;

            long durationMs = durationCol >= 0 ? cursor.GetLong(durationCol) : 0L;
            if (durationMs <= 0 && itemAndroidUri is not null)
            {
                durationMs = TryReadDurationFallback(itemAndroidUri);
            }

            if (durationMs <= 0)
            {
                seenUris.Remove(uriString);
                continue;
            }

            string? mimeType = mimeCol >= 0 ? cursor.GetString(mimeCol) : null;
            AudioFormat format = MimeTypeParser.Parse(mimeType);

            Result<AudioTrack> created = AudioTrack.Create(
                contentUri,
                format,
                artist,
                title,
                TimeSpan.FromMilliseconds(durationMs));

            if (created.TryGetOk(out AudioTrack? track))
            {
                tracks.Add(track);
            }
            else
            {
                seenUris.Remove(uriString);
            }
        }

        return (tracks, seenUris);
    }

    private static long TryReadDurationFallback(global::Android.Net.Uri androidUri)
    {
        using var retriever = new MediaMetadataRetriever();
        try
        {
            retriever.SetDataSource(Application.Context, androidUri);
            string? raw = retriever.ExtractMetadata(MetadataKey.Duration);
            return long.TryParse(raw, out long ms) ? ms : 0L;
        }
        catch
        {
            return 0L;
        }
        finally
        {
            try
            {
                retriever.Release();
            }
            catch
            {
                // Ignore release errors
            }
        }
    }

    private sealed class ReadAudioPermission : Permissions.BasePlatformPermission
    {
        public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
            Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
                ? [(Manifest.Permission.ReadMediaAudio, true)]
                : [(Manifest.Permission.ReadExternalStorage, true)];
    }
#endif
}

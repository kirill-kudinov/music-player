using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using Microsoft.Maui.Controls;
#if ANDROID
using Android.Media;
using Application = Android.App.Application;
#endif

namespace MusicPlayer.Playback;

public sealed class AndroidTrackCoverExtractor : ITrackCoverExtractor
{
    private readonly ConcurrentDictionary<string, byte[]?> _coverCache = new(StringComparer.OrdinalIgnoreCase);

    public ImageSource? ExtractCover(Uri? contentUri)
    {
        if (contentUri is null)
            return null;

        string key = contentUri.IsFile ? contentUri.LocalPath : contentUri.OriginalString;
        byte[]? cachedBytes = _coverCache.GetOrAdd(key, _ => ExtractCoverBytes(contentUri));

        return cachedBytes is { Length: > 0 }
            ? ImageSource.FromStream(() => new MemoryStream(cachedBytes, writable: false))
            : null;
    }

    private static byte[]? ExtractCoverBytes(Uri contentUri)
    {
#if ANDROID
        using var retriever = new MediaMetadataRetriever();
        try
        {
            if (contentUri.IsFile && File.Exists(contentUri.LocalPath))
            {
                retriever.SetDataSource(contentUri.LocalPath);
            }
            else
            {
                var androidUri = Android.Net.Uri.Parse(contentUri.ToString());
                if (androidUri is null)
                    return null;

                retriever.SetDataSource(Application.Context, androidUri);
            }

            byte[]? coverBytes = retriever.GetEmbeddedPicture();
            if (coverBytes is { Length: > 0 })
            {
                return coverBytes;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidTrackCoverExtractor] Ошибка извлечения обложки: {ex.Message}");
        }
        finally
        {
            try
            {
                retriever.Release();
            }
            catch
            {
                // Игнорируем ошибку освобождения нативного ресурса
            }
        }
#endif

        return null;
    }
}
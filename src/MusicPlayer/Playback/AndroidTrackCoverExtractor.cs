using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Encoding = System.Text.Encoding;
#if ANDROID
using Android.Media;
using Application = Android.App.Application;
#endif

namespace MusicPlayer.Playback;

public sealed class AndroidTrackCoverExtractor : ITrackCoverExtractor
{
    private readonly ConcurrentDictionary<string, string?> _coverFileCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _cacheDirectory = Path.Combine(FileSystem.Current.CacheDirectory, "track_covers");

    public ImageSource? ExtractCover(Uri? contentUri)
    {
        if (contentUri is null)
            return null;

        string key = (contentUri.IsAbsoluteUri && contentUri.IsFile)
            ? contentUri.LocalPath
            : contentUri.OriginalString;

        string? cachedFilePath = _coverFileCache.GetOrAdd(key, _ => ResolveOrExtractCoverFile(contentUri, key));

        if (!string.IsNullOrEmpty(cachedFilePath) && File.Exists(cachedFilePath))
            return ImageSource.FromFile(cachedFilePath);

        return null;
    }

    private string? ResolveOrExtractCoverFile(Uri contentUri, string key)
    {
        try
        {
            Directory.CreateDirectory(_cacheDirectory);

            string fileName = $"{ComputeKeyHash(key)}.img";
            string targetPath = Path.Combine(_cacheDirectory, fileName);

            if (File.Exists(targetPath) && new FileInfo(targetPath).Length > 0)
                return targetPath;

            byte[]? coverBytes = ExtractCoverBytes(contentUri);
            if (coverBytes is not { Length: > 0 })
                return null;

            File.WriteAllBytes(targetPath, coverBytes);
            return targetPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidTrackCoverExtractor] Ошибка кэширования обложки: {ex.Message}");
            return null;
        }
    }

    private static string ComputeKeyHash(string key)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hashBytes);
    }

    private static byte[]? ExtractCoverBytes(Uri contentUri)
    {
#if ANDROID
        using var retriever = new MediaMetadataRetriever();
        try
        {
            if (contentUri.IsAbsoluteUri && contentUri.IsFile && File.Exists(contentUri.LocalPath))
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
                // Ignore release errors
            }
        }
#endif

        return null;
    }
}
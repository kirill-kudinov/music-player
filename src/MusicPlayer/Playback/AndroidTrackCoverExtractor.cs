using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using Encoding = System.Text.Encoding;
using Path = System.IO.Path;
#if ANDROID
using Android.Graphics;
using Android.Media;
using Application = Android.App.Application;
using Color = Microsoft.Maui.Graphics.Color;
#endif

namespace MusicPlayer.Playback;

public sealed class AndroidTrackCoverExtractor : ITrackCoverExtractor
{
    private readonly ConcurrentDictionary<string, string?> _coverFileCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Color?> _dominantColorCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _cacheDirectory = Path.Combine(FileSystem.Current.CacheDirectory, "track_covers");

    public ImageSource? ExtractCover(Uri? contentUri)
    {
        string? cachedFilePath = GetCachedCoverFilePath(contentUri);
        if (!string.IsNullOrEmpty(cachedFilePath) && File.Exists(cachedFilePath))
            return ImageSource.FromFile(cachedFilePath);

        return null;
    }

    public string? GetCachedCoverFilePath(Uri? contentUri)
    {
        if (contentUri is null)
            return null;

        string key = GetCacheKey(contentUri);
        return _coverFileCache.GetOrAdd(key, _ => ResolveOrExtractCoverFile(contentUri, key));
    }

    public Color? ExtractDominantColor(Uri? contentUri)
    {
        if (contentUri is null)
            return null;

        string key = GetCacheKey(contentUri);
        return _dominantColorCache.GetOrAdd(key, _ => ComputeDominantColor(contentUri));
    }

    private Color? ComputeDominantColor(Uri contentUri)
    {
#if ANDROID
        string? path = GetCachedCoverFilePath(contentUri);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            var options = new BitmapFactory.Options
            {
                InSampleSize = 6,
                InPreferredConfig = Bitmap.Config.Argb8888
            };

            using Bitmap? bitmap = BitmapFactory.DecodeFile(path, options);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
                return null;

            long rSum = 0;
            long gSum = 0;
            long bSum = 0;
            long weightSum = 0;

            int stepX = Math.Max(1, bitmap.Width / 24);
            int stepY = Math.Max(1, bitmap.Height / 24);

            for (int y = 0; y < bitmap.Height; y += stepY)
            {
                for (int x = 0; x < bitmap.Width; x += stepX)
                {
                    int pixel = bitmap.GetPixel(x, y);
                    int a = Android.Graphics.Color.GetAlphaComponent(pixel);
                    if (a < 180)
                        continue;

                    int r = Android.Graphics.Color.GetRedComponent(pixel);
                    int g = Android.Graphics.Color.GetGreenComponent(pixel);
                    int b = Android.Graphics.Color.GetBlueComponent(pixel);

                    int max = Math.Max(r, Math.Max(g, b));
                    int min = Math.Min(r, Math.Min(g, b));
                    int delta = max - min;

                    // Игнорируем слишком тёмные, слишком светлые и почти серые пиксели
                    if (max < 28 || min > 230)
                        continue;

                    int weight = 1 + (delta * 3 / 32);
                    rSum += (long)r * weight;
                    gSum += (long)g * weight;
                    bSum += (long)b * weight;
                    weightSum += weight;
                }
            }

            if (weightSum == 0)
                return null;

            float avgR = (float)(rSum / (double)weightSum) / 255f;
            float avgG = (float)(gSum / (double)weightSum) / 255f;
            float avgB = (float)(bSum / (double)weightSum) / 255f;

            // Приглушаем яркость, чтобы белый текст на фоне оставался контрастным
            const float maxLuminance = 0.52f;
            float luminance = (0.299f * avgR) + (0.587f * avgG) + (0.114f * avgB);
            if (luminance > maxLuminance && luminance > 0f)
            {
                float scale = maxLuminance / luminance;
                avgR *= scale;
                avgG *= scale;
                avgB *= scale;
            }

            return Color.FromRgb(
                Math.Clamp(avgR, 0.08f, 0.85f),
                Math.Clamp(avgG, 0.08f, 0.85f),
                Math.Clamp(avgB, 0.08f, 0.85f));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidTrackCoverExtractor] Ошибка извлечения доминантного цвета: {ex.Message}");
            return null;
        }
#else
        return null;
#endif
    }

    private static string GetCacheKey(Uri contentUri)
    {
        return (contentUri.IsAbsoluteUri && contentUri.IsFile)
            ? contentUri.LocalPath
            : contentUri.OriginalString;
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
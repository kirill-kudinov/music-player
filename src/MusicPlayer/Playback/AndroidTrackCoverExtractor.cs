using System;
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
    public ImageSource? ExtractCover(Uri? contentUri)
    {
        if (contentUri is null)
            return null;

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
                return ImageSource.FromStream(() => new MemoryStream(coverBytes));
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
                //
            }
        }
#endif

        return null;
    }
}
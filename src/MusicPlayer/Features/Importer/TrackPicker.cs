using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed class TrackPicker
{
    public async Task<Result<FileResult>> PickTrackAsync()
    {
        try
        {
            var options = new PickOptions
            {
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    {
                        DevicePlatform.Android,
                        [
                            "audio/aac",
                            "audio/flac",
                            "audio/ogg",
                            "audio/opus",
                            "audio/mpeg",
                            "audio/*"
                        ]
                    }
                })
            };

            FileResult? result = await FilePicker.Default.PickAsync(options);

            if (result is null)
                return Result.Fail(ImportErrors.AudioFileNotFound);

            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackPicker] PickTrackAsync failed: {ex.Message}");
            return Result.Fail(ImportErrors.AudioFileNotFound);
        }
    }
}
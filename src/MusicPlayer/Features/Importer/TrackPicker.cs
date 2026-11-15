using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed record PickedAudioSource(string FileName, Func<Task<Stream?>> OpenReadAsync);

public sealed class TrackPicker
{
    public async Task<Result<IReadOnlyList<PickedAudioSource>>> PickTracksAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Выберите аудиофайлы",
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
                            "audio/mp4",
                            "audio/wav",
                            "audio/x-wav",
                            "audio/*"
                        ]
                    }
                })
            };

            IEnumerable<FileResult>? results = await FilePicker.Default.PickMultipleAsync(options);
            if (results is null)
                return Result.Fail(ImportErrors.AudioFileNotFound);

            List<PickedAudioSource> picked = [];
            foreach (FileResult file in results)
            {
                if (file is null)
                    continue;

                string name = string.IsNullOrWhiteSpace(file.FileName)
                    ? $"track_{Guid.NewGuid():N}.mp3"
                    : file.FileName;

                picked.Add(new PickedAudioSource(name, async () => await file.OpenReadAsync()));
            }

            if (picked.Count == 0)
                return Result.Fail(ImportErrors.AudioFileNotFound);

            return Result.Ok<IReadOnlyList<PickedAudioSource>>(picked);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[TrackPicker] PickTracksAsync failed: {ex.Message}");
            return Result.Fail(ImportErrors.AudioFileNotFound);
        }
    }
}
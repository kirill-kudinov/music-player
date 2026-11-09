using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed class TrackPicker
{
    public async Task<Result<FileResult>> PickTrackAsync()
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
                        "audio/mp3",
                        "audio/mp4"
                    ]
                }
            })
        };

        var result = await FilePicker.Default.PickAsync(options);
        
        if (result is null) return Result.Fail(ImportErrors.AudioFileNotFound);
        
        return Result.Ok(result);
    }
}
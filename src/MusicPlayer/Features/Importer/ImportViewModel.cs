using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicPlayer.Features.Importer;

public sealed partial class ImportViewModel(
    TrackPicker trackPicker,
    TrackStorage trackStorage
) : ObservableObject
{
    [RelayCommand]
    private async Task ImportTrack()
    {
        try
        {
            var pickResult = await trackPicker.PickTrackAsync();
            if (!pickResult.TryGetOk(out var getResult))
                return;

            await using Stream selectedStream = await getResult.OpenReadAsync();
            string fileName = Path.GetFileName(getResult.FileName);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"track_{Guid.NewGuid():N}.mp3";
            }

            var addResult = await trackStorage.AddTrackAsync(fileName, selectedStream);
            if (addResult.IsError)
            {
                await ShowToastSafeAsync(addResult.Error.Message);
                return;
            }

            await ShowToastSafeAsync($"Трек «{Path.GetFileNameWithoutExtension(fileName)}» добавлен");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportViewModel] ImportTrack failed: {ex.Message}");
            await ShowToastSafeAsync("Не удалось импортировать трек");
        }
    }

    private static async Task ShowToastSafeAsync(string message)
    {
        try
        {
            var toast = Toast.Make(message, ToastDuration.Short, 14d);
            await toast.Show(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportViewModel] Toast skipped: {ex.Message}");
        }
    }
}
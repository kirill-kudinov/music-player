using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public sealed partial class ImportViewModel(
    TrackPicker trackPicker,
    TrackStorage trackStorage,
    IMediaStoreAudioScanner? mediaStoreScanner = null
) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartImport))]
    private bool _isScanningMediaStore;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartImport))]
    private bool _isImportingFiles;

    [ObservableProperty]
    private double _scanProgress;

    [ObservableProperty]
    private string _scanStatusText = string.Empty;

    [ObservableProperty]
    private double _filesImportProgress;

    [ObservableProperty]
    private string _filesStatusText = string.Empty;

    public bool CanStartImport => !IsScanningMediaStore && !IsImportingFiles;

    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task ScanMediaStoreAsync()
    {
        if (mediaStoreScanner is null)
            return;

        try
        {
            IsScanningMediaStore = true;
            ScanProgress = 0.0;
            ScanStatusText = "Поиск треков...";

            var progress = new Progress<MediaScanProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (p.Total > 0)
                    {
                        ScanProgress = (double)p.Processed / p.Total;
                        ScanStatusText = $"Найдено: {p.Processed} / {p.Total}";
                    }
                });
            });

            Result<IReadOnlyList<AudioTrack>> result = await mediaStoreScanner.ScanDeviceAudioAsync(progress);
            if (!result.TryGetOk(out IReadOnlyList<AudioTrack>? tracks))
            {
                await ShowToastSafeAsync("Нет доступа к аудиофайлам или треки не найдены");
                return;
            }

            ScanProgress = 1.0;
            await ShowToastSafeAsync($"Медиатека обновлена: {tracks.Count} трек(ов)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportViewModel] ScanMediaStoreAsync failed: {ex.Message}");
            await ShowToastSafeAsync("Не удалось просканировать медиатеку");
        }
        finally
        {
            IsScanningMediaStore = false;
            ScanProgress = 0.0;
            ScanStatusText = string.Empty;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartImport))]
    private async Task ImportTracksAsync()
    {
        try
        {
            var pickResult = await trackPicker.PickTracksAsync();
            if (!pickResult.TryGetOk(out IReadOnlyList<PickedAudioSource>? files) || files.Count == 0)
                return;

            IsImportingFiles = true;
            FilesImportProgress = 0.0;
            int total = files.Count;
            int imported = 0;

            for (int i = 0; i < total; i++)
            {
                PickedAudioSource source = files[i];
                try
                {
                    await using Stream? stream = await source.OpenReadAsync();
                    if (stream is not null)
                    {
                        string fileName = Path.GetFileName(source.FileName);
                        if (string.IsNullOrWhiteSpace(fileName))
                        {
                            fileName = $"track_{Guid.NewGuid():N}.mp3";
                        }

                        var addResult = await trackStorage.AddTrackAsync(fileName, stream);
                        if (addResult.IsOk)
                        {
                            imported++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImportViewModel] Failed to import '{source.FileName}': {ex.Message}");
                }

                FilesImportProgress = (double)(i + 1) / total;
                FilesStatusText = $"{i + 1} / {total}";
            }

            if (imported > 0)
            {
                await ShowToastSafeAsync($"Импортировано треков: {imported}");
            }
            else
            {
                await ShowToastSafeAsync("Не удалось импортировать выбранные файлы");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ImportViewModel] ImportTracksAsync failed: {ex.Message}");
            await ShowToastSafeAsync("Не удалось импортировать треки");
        }
        finally
        {
            IsImportingFiles = false;
            FilesImportProgress = 0.0;
            FilesStatusText = string.Empty;
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
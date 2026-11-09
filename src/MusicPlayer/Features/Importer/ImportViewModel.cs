using System.IO;
using System.Threading.Tasks;
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
        var pickResult = await trackPicker.PickTrackAsync();

        if (!pickResult.TryGetOk(out var getResult))
            return;

        await using Stream selectedStream = await getResult.OpenReadAsync();

        await trackStorage.AddTrackAsync(getResult.FileName, selectedStream);
    }
}
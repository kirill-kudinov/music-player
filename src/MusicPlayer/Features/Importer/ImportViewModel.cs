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

        if (!pickResult.TryGetOk(out var getResult)) return;

        var selectedFile = (FileStream)await getResult.OpenReadAsync();

        await trackStorage.AddTrackAsync(getResult.FileName, selectedFile);
    }
}
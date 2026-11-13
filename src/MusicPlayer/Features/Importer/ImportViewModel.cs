using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Features.Importer;

public sealed partial class ImportViewModel(
    Database database,
    TrackPicker trackPicker,
    FileStorage fileStorage
) : ObservableObject
{
    [RelayCommand]
    private async Task ImportTrack()
    {
        var pickResult = await trackPicker.PickTrackAsync();

        if (!pickResult.TryGetOk(out var getResult)) return;

        var selectedFile = (FileStream)await getResult.OpenReadAsync();

        await fileStorage.AddAsync(getResult.FileName, selectedFile);

        await database.AddAudioTrack(AudioTrack.Create(selectedFile.Name));
    }
}
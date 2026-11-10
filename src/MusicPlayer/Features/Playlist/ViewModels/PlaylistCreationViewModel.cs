using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicPlayer.Features.Playlist.ViewModels;

public sealed partial class CreatePlaylistViewModel : ObservableObject
{
    [ObservableProperty] public partial string PlaylistName { get; set; } = string.Empty;
    
    [RelayCommand]
    public void Cancel() {}
    
    [RelayCommand]
    public void Create() {}
}
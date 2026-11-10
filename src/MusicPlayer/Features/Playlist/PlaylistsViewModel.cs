using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicPlayer.Features.Playlist;

public sealed partial class AvailablePlaylistsViewModel : ObservableObject
{
    public ObservableCollection<Core.Playlist.Playlist> Playlists = [];

    [RelayCommand]
    public void AddNewPlaylist()
    {
        
    }

    [RelayCommand]
    public void GoToPlaylist()
    {
        
    }
}
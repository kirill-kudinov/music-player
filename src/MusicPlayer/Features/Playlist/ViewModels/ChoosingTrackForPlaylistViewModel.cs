using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicPlayer.Features.Playlist.ViewModels;

public sealed partial class ChoosingTrackForPlaylist : ObservableObject
{
    public ObservableCollection<Core.Playlist.Playlist> Playlists = [];
    
    [RelayCommand]
    public void Choose() {}
}
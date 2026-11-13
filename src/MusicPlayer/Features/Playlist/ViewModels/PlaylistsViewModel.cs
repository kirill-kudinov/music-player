using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Playlist.Pages;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Features.Playlist.ViewModels;

public sealed partial class PlaylistsViewModel(
    Database database
) : ObservableObject
{
    [ObservableProperty] public partial ObservableCollection<Core.Playlist.Playlist> Playlists { get; set; } = [];

    [RelayCommand]
    private async Task OpenPlaylist(Core.Playlist.Playlist? playlist)
    {
        if (playlist is null) return;

        await Shell.Current.GoToAsync(
            nameof(PlaylistPage),
            new Dictionary<string, object>
            {
                { "Playlist", playlist }
            }
        );
    }

    [RelayCommand]
    private async Task CreateNewPlaylist()
    {
        await Shell.Current.GoToAsync(nameof(PlaylistCreationPage));
    }

    public async Task LoadPlaylists()
    {
        Playlists = [.. await database.GetPlaylists()];
    }
}
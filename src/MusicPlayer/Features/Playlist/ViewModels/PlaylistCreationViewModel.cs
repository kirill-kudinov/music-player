using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Playlist.Pages;
using MusicPlayer.Infrastructure;

namespace MusicPlayer.Features.Playlist.ViewModels;

public sealed partial class PlaylistCreationViewModel(
    Database database
) : ObservableObject
{
    [ObservableProperty] public partial string PlaylistName { get; set; } = string.Empty;

    [RelayCommand]
    private async Task CancelAsync() => await Shell.Current.GoToAsync($"//{nameof(PlaylistsPage)}");

    [RelayCommand]
    private async Task CreateAsync()
    {
        var createResult = Core.Playlist.Playlist.Create(PlaylistName);

        if (!createResult.TryGetOk(out var playlist)) return;

        await database.AddPlaylist(playlist);

        await Shell.Current.GoToAsync($"//{nameof(PlaylistsPage)}");
    }
}
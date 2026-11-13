using Microsoft.Maui.Controls;
using MusicPlayer.Features.Playlist.ViewModels;

namespace MusicPlayer.Features.Playlist.Pages;

public partial class PlaylistCreationPage : ContentPage
{
    public PlaylistCreationPage(PlaylistCreationViewModel playlistCreationViewModel)
    {
        InitializeComponent();

        BindingContext = playlistCreationViewModel;
    }
}
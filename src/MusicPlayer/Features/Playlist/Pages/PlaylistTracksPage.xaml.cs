using Microsoft.Maui.Controls;
using MusicPlayer.Features.Playlist.ViewModels;

namespace MusicPlayer.Features.Playlist.Pages;

public partial class PlaylistTracks : ContentPage
{
    public PlaylistTracks(PlaylistTracksViewModel playlistTracksViewModel)
    {
        InitializeComponent();

        BindingContext = playlistTracksViewModel;
    }
}
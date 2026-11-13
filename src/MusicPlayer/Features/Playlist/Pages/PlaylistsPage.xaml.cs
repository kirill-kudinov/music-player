using Microsoft.Maui.Controls;
using MusicPlayer.Features.Playlist.ViewModels;

namespace MusicPlayer.Features.Playlist.Pages;

public partial class PlaylistsPage : ContentPage
{
    private PlaylistsViewModel PlaylistsViewModel { get; }

    public PlaylistsPage(PlaylistsViewModel playlistsViewModel)
    {
        InitializeComponent();

        PlaylistsViewModel = playlistsViewModel;

        BindingContext = playlistsViewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await PlaylistsViewModel.LoadPlaylists();
    }
}
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Playlist.Pages;

namespace MusicPlayer;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(PlayerPage), typeof(PlayerPage));
        
        Routing.RegisterRoute(nameof(PlaylistsPage), typeof(PlaylistsPage));
        Routing.RegisterRoute(nameof(PlaylistCreationPage), typeof(PlaylistCreationPage));
        Routing.RegisterRoute(nameof(PlaylistPage), typeof(PlaylistPage));
        Routing.RegisterRoute(nameof(PlaylistChoosingTrackPage), typeof(PlaylistChoosingTrackPage));
    }
}
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Playlists;

namespace MusicPlayer;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(PlayerPage), typeof(PlayerPage));
        Routing.RegisterRoute(nameof(PlaylistDetailPage), typeof(PlaylistDetailPage));
    }
}
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player;

namespace MusicPlayer;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(PlayerPage), typeof(PlayerPage));
    }
}
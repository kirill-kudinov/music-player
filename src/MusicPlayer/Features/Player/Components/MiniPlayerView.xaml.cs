using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;

namespace MusicPlayer.Features.Player.Components;

public partial class MiniPlayerView : ContentView
{
    public MiniPlayerView()
    {
        InitializeComponent();
    }

    public Task PlayExpandOutAnimationAsync()
    {
        return PlayerVisualAnimator.AnimateMiniPlayerExpandOutAsync(MiniPlayerContainer);
    }

    public Task PlayCollapseInAnimationAsync()
    {
        return PlayerVisualAnimator.AnimateMiniPlayerCollapseInAsync(MiniPlayerContainer);
    }
}
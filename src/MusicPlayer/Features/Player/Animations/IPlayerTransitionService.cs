namespace MusicPlayer.Features.Player.Animations;

using System;
using System.Threading.Tasks;


public interface IPlayerTransitionService
{
    bool IsTransitioning { get; }

    void RegisterPlayerPageCallbacks(
        Func<Task> playCollapseAnimationAsync);

    void UnregisterPlayerPageCallbacks();

    void RegisterMiniPlayerCallbacks(
        Func<Task> playExpandOutAnimationAsync,
        Func<Task> playCollapseInAnimationAsync);

    void UnregisterMiniPlayerCallbacks();

    Task ExpandAsync();

    Task CollapseAsync();
}
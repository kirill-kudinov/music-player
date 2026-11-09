using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace MusicPlayer.Features.Player.Animations;


public sealed class PlayerTransitionService : IPlayerTransitionService
{
    private readonly SemaphoreSlim _transitionLock = new(1, 1);

    private Func<Task>? _playPlayerCollapseAnimationAsync;
    private Func<Task>? _playMiniPlayerExpandOutAnimationAsync;
    private Func<Task>? _playMiniPlayerCollapseInAnimationAsync;

    private bool _isExpanded;

    public bool IsTransitioning { get; private set; }

    public void RegisterPlayerPageCallbacks(Func<Task> playCollapseAnimationAsync)
    {
        _playPlayerCollapseAnimationAsync = playCollapseAnimationAsync;
        _isExpanded = true;
    }

    public void UnregisterPlayerPageCallbacks()
    {
        _playPlayerCollapseAnimationAsync = null;
        _isExpanded = false;
    }

    public void RegisterMiniPlayerCallbacks(
        Func<Task> playExpandOutAnimationAsync,
        Func<Task> playCollapseInAnimationAsync)
    {
        _playMiniPlayerExpandOutAnimationAsync = playExpandOutAnimationAsync;
        _playMiniPlayerCollapseInAnimationAsync = playCollapseInAnimationAsync;
    }

    public void UnregisterMiniPlayerCallbacks()
    {
        _playMiniPlayerExpandOutAnimationAsync = null;
        _playMiniPlayerCollapseInAnimationAsync = null;
    }

    public async Task ExpandAsync()
    {
        if (IsTransitioning || _isExpanded)
            return;

        if (!await _transitionLock.WaitAsync(0))
            return;

        try
        {
            IsTransitioning = true;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (_playMiniPlayerExpandOutAnimationAsync is not null)
                {
                    await _playMiniPlayerExpandOutAnimationAsync();
                }

                if (Shell.Current is not null)
                {
                    await Shell.Current.GoToAsync(nameof(PlayerPage), animate: false);
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            IsTransitioning = false;
            _transitionLock.Release();
        }
    }

    public async Task CollapseAsync()
    {
        if (IsTransitioning || !_isExpanded)
            return;

        if (!await _transitionLock.WaitAsync(0))
            return;

        try
        {
            IsTransitioning = true;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (_playPlayerCollapseAnimationAsync is not null)
                {
                    await _playPlayerCollapseAnimationAsync();
                }

                if (Shell.Current is not null)
                {
                    await Shell.Current.GoToAsync("..", animate: false);
                }

                if (_playMiniPlayerCollapseInAnimationAsync is not null)
                {
                    await _playMiniPlayerCollapseInAnimationAsync();
                }
            });
        }
        finally
        {
            IsTransitioning = false;
            _transitionLock.Release();
        }
    }
}
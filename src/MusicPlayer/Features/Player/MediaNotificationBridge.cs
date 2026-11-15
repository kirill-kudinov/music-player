using System;
using Microsoft.Maui.ApplicationModel;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Player;

public sealed class MediaNotificationBridge : IDisposable
{
    private readonly IMediaNotificationService? _service;
    private readonly Action _onPlay;
    private readonly Action _onPause;
    private readonly Action _onNext;
    private readonly Action _onPrevious;
    private readonly Action<TimeSpan> _onSeek;
    private readonly Action _onStop;

    public MediaNotificationBridge(
        IMediaNotificationService? service,
        Action onPlay,
        Action onPause,
        Action onNext,
        Action onPrevious,
        Action<TimeSpan> onSeek,
        Action onStop)
    {
        _service = service;
        _onPlay = onPlay;
        _onPause = onPause;
        _onNext = onNext;
        _onPrevious = onPrevious;
        _onSeek = onSeek;
        _onStop = onStop;

        if (_service is not null)
        {
            _service.PlayRequested += HandlePlay;
            _service.PauseRequested += HandlePause;
            _service.NextRequested += HandleNext;
            _service.PreviousRequested += HandlePrevious;
            _service.SeekRequested += HandleSeek;
            _service.StopRequested += HandleStop;
        }
    }

    public void Sync(AudioTrack? track, bool isPlaying, TimeSpan position)
    {
        if (_service is null)
            return;

        if (track is null)
        {
            _service.Clear();
        }
        else
        {
            _service.UpdatePlayback(track, isPlaying, position);
        }
    }

    public void Clear() => _service?.Clear();

    private void HandlePlay(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(_onPlay);
    private void HandlePause(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(_onPause);
    private void HandleNext(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(_onNext);
    private void HandlePrevious(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(_onPrevious);
    private void HandleSeek(object? sender, TimeSpan pos) => MainThread.BeginInvokeOnMainThread(() => _onSeek(pos));
    private void HandleStop(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(_onStop);

    public void Dispose()
    {
        if (_service is null)
            return;

        _service.PlayRequested -= HandlePlay;
        _service.PauseRequested -= HandlePause;
        _service.NextRequested -= HandleNext;
        _service.PreviousRequested -= HandlePrevious;
        _service.SeekRequested -= HandleSeek;
        _service.StopRequested -= HandleStop;
    }
}

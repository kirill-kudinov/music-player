using System;
using System.Diagnostics;
using System.IO;
using Debug = System.Diagnostics.Debug;
using Environment = System.Environment;
#if ANDROID
using Android.Media;
using Android.OS;
using Application = Android.App.Application;
#endif

namespace MusicPlayer.Playback;

public sealed class AndroidAudioPlayer : IAudioPlayer, IDisposable
{
#if ANDROID
    private const long SeekStabilizationMs = 300;

    private readonly object _syncRoot = new();
    private MediaPlayer? _mediaPlayer;
    private bool _isPrepared;
    private bool _isSeeking;
    private long _seekCompletedTimestampMs;
    private TimeSpan _lastSeekPosition;
    private TimeSpan? _pendingSeekPosition;
    private TimeSpan _cachedDuration;
#endif

    public event EventHandler? PlaybackCompleted;

    public bool IsPlaying
    {
        get
        {
#if ANDROID
            lock (_syncRoot)
            {
                try
                {
                    return _mediaPlayer is { IsPlaying: true };
                }
                catch
                {
                    return false;
                }
            }
#else
            return false;
#endif
        }
    }

    public TimeSpan Position
    {
        get
        {
#if ANDROID
            lock (_syncRoot)
            {
                if (_mediaPlayer is null || !_isPrepared)
                    return _pendingSeekPosition ?? TimeSpan.Zero;

                if (_isSeeking)
                    return _lastSeekPosition;

                // Защита от возврата старой позиции нативным MediaPlayer сразу после SeekComplete
                if (Environment.TickCount64 - _seekCompletedTimestampMs < SeekStabilizationMs)
                    return _lastSeekPosition;

                try
                {
                    int currentMs = _mediaPlayer.CurrentPosition;
                    return currentMs > 0 ? TimeSpan.FromMilliseconds(currentMs) : TimeSpan.Zero;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AudioPlayer] Error reading Position: {ex.Message}");
                    return _lastSeekPosition;
                }
            }
#else
            return TimeSpan.Zero;
#endif
        }
    }

    public TimeSpan Duration
    {
        get
        {
#if ANDROID
            lock (_syncRoot)
            {
                if (_cachedDuration > TimeSpan.Zero)
                    return _cachedDuration;

                if (_mediaPlayer is null || !_isPrepared)
                    return TimeSpan.Zero;

                try
                {
                    int durationMs = _mediaPlayer.Duration;
                    if (durationMs > 0)
                    {
                        _cachedDuration = TimeSpan.FromMilliseconds(durationMs);
                        return _cachedDuration;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AudioPlayer] Error reading Duration: {ex.Message}");
                }

                return TimeSpan.Zero;
            }
#else
            return TimeSpan.Zero;
#endif
        }
    }

    public void Play(Uri source)
    {
#if ANDROID
        ArgumentNullException.ThrowIfNull(source);

        lock (_syncRoot)
        {
            StopInternal();

            _isPrepared = false;
            _isSeeking = false;
            _seekCompletedTimestampMs = 0;
            _pendingSeekPosition = null;
            _lastSeekPosition = TimeSpan.Zero;
            _cachedDuration = TimeSpan.Zero;

            var player = new MediaPlayer();
            _mediaPlayer = player;

            using var attributes = new AudioAttributes.Builder()
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!;

            player.SetAudioAttributes(attributes);

            player.Prepared += OnPlayerPrepared;
            player.SeekComplete += OnPlayerSeekComplete;
            player.Completion += OnPlayerCompletion;

            try
            {
                if (source.IsFile && File.Exists(source.LocalPath))
                {
                    player.SetDataSource(source.LocalPath);
                }
                else
                {
                    var androidUri = Android.Net.Uri.Parse(source.ToString())!;
                    player.SetDataSource(Application.Context, androidUri);
                }

                player.PrepareAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayer] Failed to load Uri: {ex.Message}");
                StopInternal();
            }
        }
#endif
    }

    public void Resume()
    {
#if ANDROID
        lock (_syncRoot)
        {
            if (_mediaPlayer is not null && _isPrepared && !_mediaPlayer.IsPlaying)
            {
                _mediaPlayer.Start();
            }
        }
#endif
    }

    public void Pause()
    {
#if ANDROID
        lock (_syncRoot)
        {
            if (_mediaPlayer is { IsPlaying: true })
            {
                _mediaPlayer.Pause();
            }
        }
#endif
    }

    public void Seek(TimeSpan position)
    {
#if ANDROID
        lock (_syncRoot)
        {
            if (_mediaPlayer is null)
                return;

            TimeSpan clampedPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;

            if (!_isPrepared)
            {
                _pendingSeekPosition = clampedPosition;
                _lastSeekPosition = clampedPosition;
                return;
            }

            try
            {
                int durationMs = _cachedDuration > TimeSpan.Zero
                    ? (int)_cachedDuration.TotalMilliseconds
                    : _mediaPlayer.Duration;

                long targetMs = durationMs > 0
                    ? Math.Clamp((long)clampedPosition.TotalMilliseconds, 0L, durationMs)
                    : Math.Max(0L, (long)clampedPosition.TotalMilliseconds);

                _lastSeekPosition = TimeSpan.FromMilliseconds(targetMs);
                _isSeeking = true;
                _seekCompletedTimestampMs = Environment.TickCount64;

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    _mediaPlayer.SeekTo(targetMs, MediaPlayerSeekMode.ClosestSync);
                }
                else
                {
                    _mediaPlayer.SeekTo((int)targetMs);
                }
            }
            catch (Exception ex)
            {
                _isSeeking = false;
                Debug.WriteLine($"[AudioPlayer] Error seeking: {ex.Message}");
            }
        }
#endif
    }

    public void Stop()
    {
#if ANDROID
        lock (_syncRoot)
        {
            StopInternal();
        }
#endif
    }

#if ANDROID
    private void OnPlayerPrepared(object? sender, EventArgs e)
    {
        lock (_syncRoot)
        {
            if (_mediaPlayer is null || !ReferenceEquals(sender, _mediaPlayer))
                return;

            _isPrepared = true;

            int durationMs = _mediaPlayer.Duration;
            if (durationMs > 0)
            {
                _cachedDuration = TimeSpan.FromMilliseconds(durationMs);
            }

            if (_pendingSeekPosition is { } pendingSeek)
            {
                _pendingSeekPosition = null;
                long targetMs = durationMs > 0
                    ? Math.Clamp((long)pendingSeek.TotalMilliseconds, 0L, durationMs)
                    : Math.Max(0L, (long)pendingSeek.TotalMilliseconds);

                _lastSeekPosition = TimeSpan.FromMilliseconds(targetMs);
                _isSeeking = true;
                _seekCompletedTimestampMs = Environment.TickCount64;

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    _mediaPlayer.SeekTo(targetMs, MediaPlayerSeekMode.ClosestSync);
                }
                else
                {
                    _mediaPlayer.SeekTo((int)targetMs);
                }
            }

            _mediaPlayer.Start();
        }
    }

    private void OnPlayerSeekComplete(object? sender, EventArgs e)
    {
        lock (_syncRoot)
        {
            if (!ReferenceEquals(sender, _mediaPlayer))
                return;

            _isSeeking = false;
            _seekCompletedTimestampMs = Environment.TickCount64;
        }
    }

    private void OnPlayerCompletion(object? sender, EventArgs e)
    {
        lock (_syncRoot)
        {
            if (!ReferenceEquals(sender, _mediaPlayer))
                return;

            _isSeeking = false;
        }

        PlaybackCompleted?.Invoke(this, EventArgs.Empty);
    }

    private void StopInternal()
    {
        if (_mediaPlayer is null)
            return;

        var player = _mediaPlayer;
        _mediaPlayer = null;
        _isPrepared = false;
        _isSeeking = false;
        _pendingSeekPosition = null;
        _cachedDuration = TimeSpan.Zero;

        player.Prepared -= OnPlayerPrepared;
        player.SeekComplete -= OnPlayerSeekComplete;
        player.Completion -= OnPlayerCompletion;

        try
        {
            if (player.IsPlaying)
                player.Stop();

            player.Reset();
            player.Release();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayer] Error stopping MediaPlayer: {ex.Message}");
        }
        finally
        {
            player.Dispose();
        }
    }
#endif

    public void Dispose() => Stop();
}
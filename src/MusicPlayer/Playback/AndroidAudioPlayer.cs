using System;
using System.Diagnostics;
using System.IO;
#if ANDROID
using Android.Media;
using Application = Android.App.Application;
#endif

namespace MusicPlayer.Playback;

public sealed class AndroidAudioPlayer : IAudioPlayer, IDisposable
{
#if ANDROID
    private MediaPlayer? _mediaPlayer;
    private bool _isPrepared;
    private bool _isSeeking;
    private TimeSpan _lastSeekPosition;
    private TimeSpan? _pendingSeekPosition;
#endif

    public event EventHandler? PlaybackCompleted;

    public bool IsPlaying
    {
        get
        {
#if ANDROID
            return _mediaPlayer is { IsPlaying: true };
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
            if (_mediaPlayer is null || !_isPrepared)
                return _pendingSeekPosition ?? TimeSpan.Zero;

            if (_isSeeking)
                return _lastSeekPosition;

            try
            {
                return TimeSpan.FromMilliseconds(_mediaPlayer.CurrentPosition);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayer] Error reading Position: {ex.Message}");
                return TimeSpan.Zero;
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
            if (_mediaPlayer is null || !_isPrepared)
                return TimeSpan.Zero;

            try
            {
                int durationMs = _mediaPlayer.Duration;
                return durationMs > 0 ? TimeSpan.FromMilliseconds(durationMs) : TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayer] Error reading Duration: {ex.Message}");
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
        Stop();
        _isPrepared = false;
        _isSeeking = false;
        _pendingSeekPosition = null;
        _lastSeekPosition = TimeSpan.Zero;

        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!
        );

        _mediaPlayer.Prepared += (_, _) =>
        {
            if (_mediaPlayer is null)
                return;

            _isPrepared = true;

            if (_pendingSeekPosition is { } pendingSeek)
            {
                _pendingSeekPosition = null;
                int targetMs = (int)Math.Clamp(pendingSeek.TotalMilliseconds, 0, Math.Max(0, _mediaPlayer.Duration));
                _mediaPlayer.SeekTo(targetMs);
            }

            _mediaPlayer.Start();
        };

        _mediaPlayer.SeekComplete += (_, _) =>
        {
            _isSeeking = false;
        };

        _mediaPlayer.Completion += (_, _) =>
        {
            _isSeeking = false;
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        };

        try
        {
            if (source.IsFile && File.Exists(source.LocalPath))
            {
                _mediaPlayer.SetDataSource(source.LocalPath);
            }
            else
            {
                var androidUri = Android.Net.Uri.Parse(source.ToString())!;
                _mediaPlayer.SetDataSource(Application.Context, androidUri);
            }

            _mediaPlayer.PrepareAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayer] Failed to load Uri: {ex.Message}");
            Stop();
        }
#endif
    }

    public void Resume()
    {
#if ANDROID
        if (_mediaPlayer is not null && _isPrepared && !_mediaPlayer.IsPlaying)
            _mediaPlayer.Start();
#endif
    }

    public void Pause()
    {
#if ANDROID
        if (_mediaPlayer is { IsPlaying: true })
            _mediaPlayer.Pause();
#endif
    }

    public void Seek(TimeSpan position)
    {
#if ANDROID
        if (_mediaPlayer is null)
            return;

        if (!_isPrepared)
        {
            _pendingSeekPosition = position;
            return;
        }

        try
        {
            int durationMs = _mediaPlayer.Duration;
            int ms = durationMs > 0
                ? (int)Math.Clamp(position.TotalMilliseconds, 0, durationMs)
                : (int)Math.Max(0, position.TotalMilliseconds);

            _lastSeekPosition = TimeSpan.FromMilliseconds(ms);
            _isSeeking = true;
            _mediaPlayer.SeekTo(ms);
        }
        catch (Exception ex)
        {
            _isSeeking = false;
            Debug.WriteLine($"[AudioPlayer] Error seeking: {ex.Message}");
        }
#endif
    }

    public void Stop()
    {
#if ANDROID
        if (_mediaPlayer is null)
            return;

        _isPrepared = false;
        _isSeeking = false;
        _pendingSeekPosition = null;

        try
        {
            if (_mediaPlayer.IsPlaying)
                _mediaPlayer.Stop();

            _mediaPlayer.Reset();
            _mediaPlayer.Release();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayer] Error stopping MediaPlayer: {ex.Message}");
        }
        finally
        {
            _mediaPlayer.Dispose();
            _mediaPlayer = null;
        }
#endif
    }

    public void Dispose() => Stop();
}
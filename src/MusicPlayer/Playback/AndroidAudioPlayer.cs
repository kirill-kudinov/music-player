using System;
using System.Diagnostics;
using Android.Media;
#if ANDROID
using Application = Android.App.Application;
#endif

namespace MusicPlayer.Playback;

public sealed class AndroidAudioPlayer : IAudioPlayer, IDisposable
{
#if ANDROID
    private MediaPlayer? _mediaPlayer;
    private bool _isPrepared;
#endif

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
                return TimeSpan.Zero;

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
                return TimeSpan.FromMilliseconds(_mediaPlayer.Duration);
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

        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.SetAudioAttributes(
            new AudioAttributes.Builder()!
                .SetContentType(AudioContentType.Music)!
                .SetUsage(AudioUsageKind.Media)!
                .Build()!
        );

        _mediaPlayer.Prepared += (sender, args) =>
        {
            _isPrepared = true;
            _mediaPlayer.Start();
        };

        try
        {
            var androidUri = Android.Net.Uri.Parse(source.ToString())!;
            _mediaPlayer.SetDataSource(Application.Context, androidUri);
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
        if (_mediaPlayer is not null && _isPrepared)
        {
            int ms = (int)Math.Clamp(position.TotalMilliseconds, 0, _mediaPlayer.Duration);
            _mediaPlayer.SeekTo(ms);
        }
#endif
    }

    public void Stop()
    {
#if ANDROID
        if (_mediaPlayer is null)
            return;

        _isPrepared = false;
        try
        {
            if (_mediaPlayer.IsPlaying)
                _mediaPlayer.Stop();

            _mediaPlayer.Reset();
            _mediaPlayer.Release();
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
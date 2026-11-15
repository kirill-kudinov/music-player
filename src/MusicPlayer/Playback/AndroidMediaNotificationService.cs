using System;
using System.Diagnostics;
using MusicPlayer.Core.AudioTracks;
using Debug = System.Diagnostics.Debug;
#if ANDROID
using Android.Content;
using Android.OS;
using Application = Android.App.Application;
#endif

namespace MusicPlayer.Playback;

public sealed class AndroidMediaNotificationService : IMediaNotificationService
{
    private readonly ITrackCoverExtractor _coverExtractor;

    public static AndroidMediaNotificationService? Current { get; private set; }

    public AudioTrack? CurrentTrack { get; private set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Position { get; private set; }
    public string? CoverFilePath { get; private set; }

    public event EventHandler? PlayRequested;
    public event EventHandler? PauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? PreviousRequested;
    public event EventHandler<TimeSpan>? SeekRequested;
    public event EventHandler? StopRequested;

    public AndroidMediaNotificationService(ITrackCoverExtractor coverExtractor)
    {
        _coverExtractor = coverExtractor;
        Current = this;
    }

    public void UpdatePlayback(AudioTrack? track, bool isPlaying, TimeSpan position)
    {
        CurrentTrack = track;
        IsPlaying = isPlaying;
        Position = position;
        CoverFilePath = track is not null ? _coverExtractor.GetCachedCoverFilePath(track.ContentUri) : null;

        if (track is null)
        {
            Clear();
            return;
        }

#if ANDROID
        try
        {
            Context context = Application.Context;
            var intent = new Intent(context, typeof(MediaPlaybackForegroundService));

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidMediaNotificationService] Failed to start/update service: {ex.Message}");
        }
#endif
    }

    public void Clear()
    {
        CurrentTrack = null;
        IsPlaying = false;
        Position = TimeSpan.Zero;
        CoverFilePath = null;

#if ANDROID
        try
        {
            Context context = Application.Context;
            var intent = new Intent(context, typeof(MediaPlaybackForegroundService));
            context.StopService(intent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AndroidMediaNotificationService] Failed to stop service: {ex.Message}");
        }
#endif
    }

    internal void RaisePlayRequested() => PlayRequested?.Invoke(this, EventArgs.Empty);
    internal void RaisePauseRequested() => PauseRequested?.Invoke(this, EventArgs.Empty);
    internal void RaiseNextRequested() => NextRequested?.Invoke(this, EventArgs.Empty);
    internal void RaisePreviousRequested() => PreviousRequested?.Invoke(this, EventArgs.Empty);
    internal void RaiseSeekRequested(TimeSpan position) => SeekRequested?.Invoke(this, position);
    internal void RaiseStopRequested() => StopRequested?.Invoke(this, EventArgs.Empty);
}

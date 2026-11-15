using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Playback;
using AudioTrack = MusicPlayer.Core.AudioTracks.AudioTrack;
using Debug = System.Diagnostics.Debug;

namespace MusicPlayer;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class MediaPlaybackForegroundService : Service
{
    public const string ChannelId = "music_player_playback_channel";
    public const int NotificationId = 1001;

    public const string ActionPlay = "com.music.player.action.PLAY";
    public const string ActionPause = "com.music.player.action.PAUSE";
    public const string ActionNext = "com.music.player.action.NEXT";
    public const string ActionPrevious = "com.music.player.action.PREVIOUS";
    public const string ActionStop = "com.music.player.action.STOP";

    private MediaSession? _mediaSession;
    private AndroidMediaNotificationService? _coordinator;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        EnsureNotificationChannel();

        _coordinator = AndroidMediaNotificationService.Current;
        _mediaSession = new MediaSession(this, "MusicPlayerMediaSession");
        _mediaSession.SetCallback(new SessionCallback(this));
        _mediaSession.Active = true;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        string? action = intent?.Action;
        if (!string.IsNullOrEmpty(action))
        {
            HandleAction(action);
            return StartCommandResult.Sticky;
        }

        UpdateNotificationFromCoordinator();
        return StartCommandResult.Sticky;
    }

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        base.OnTaskRemoved(rootIntent);
        StopForegroundAndSelf();
    }

    public override void OnDestroy()
    {
        if (_mediaSession is not null)
        {
            _mediaSession.Active = false;
            _mediaSession.Release();
            _mediaSession.Dispose();
            _mediaSession = null;
        }

        base.OnDestroy();
    }

    private void HandleAction(string action)
    {
        switch (action)
        {
            case ActionPlay:
                _coordinator?.RaisePlayRequested();
                break;
            case ActionPause:
                _coordinator?.RaisePauseRequested();
                break;
            case ActionNext:
                _coordinator?.RaiseNextRequested();
                break;
            case ActionPrevious:
                _coordinator?.RaisePreviousRequested();
                break;
            case ActionStop:
                _coordinator?.RaiseStopRequested();
                StopForegroundAndSelf();
                break;
        }
    }

    private void UpdateNotificationFromCoordinator()
    {
        if (_coordinator is null || _mediaSession is null)
            return;

        AudioTrack? track = _coordinator.CurrentTrack;
        if (track is null)
        {
            StopForegroundAndSelf();
            return;
        }

        bool isPlaying = _coordinator.IsPlaying;
        TimeSpan position = _coordinator.Position;
        Bitmap? albumArt = LoadAlbumArt(_coordinator.CoverFilePath);

        try
        {
            using var metadataBuilder = new MediaMetadata.Builder()
                .PutString(MediaMetadata.MetadataKeyTitle, track.Title)!
                .PutString(MediaMetadata.MetadataKeyArtist, track.Artist)!
                .PutLong(MediaMetadata.MetadataKeyDuration, (long)track.Duration.TotalMilliseconds)!;

            if (albumArt is not null)
            {
                metadataBuilder.PutBitmap(MediaMetadata.MetadataKeyAlbumArt, albumArt);
            }

            _mediaSession.SetMetadata(metadataBuilder.Build());

            long actions =
                PlaybackState.ActionPlay |
                PlaybackState.ActionPause |
                PlaybackState.ActionPlayPause |
                PlaybackState.ActionSkipToNext |
                PlaybackState.ActionSkipToPrevious |
                PlaybackState.ActionSeekTo |
                PlaybackState.ActionStop;

            PlaybackStateCode stateCode = isPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused;
            using var stateBuilder = new PlaybackState.Builder()
                .SetActions(actions)!
                .SetState(stateCode, (long)position.TotalMilliseconds, isPlaying ? 1.0f : 0.0f)!;

            _mediaSession.SetPlaybackState(stateBuilder.Build());

            Notification notification = BuildNotification(track, isPlaying, albumArt);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                StartForeground(NotificationId, notification, ForegroundService.TypeMediaPlayback);
            }
            else
            {
                StartForeground(NotificationId, notification);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaPlaybackForegroundService] Update failed: {ex.Message}");
        }
        finally
        {
            albumArt?.Dispose();
        }
    }

    private Notification BuildNotification(AudioTrack track, bool isPlaying, Bitmap? albumArt)
    {
        var launchIntent = new Intent(this, typeof(MainActivity));
        launchIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        PendingIntentFlags piFlags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable;
        PendingIntent? contentPendingIntent = PendingIntent.GetActivity(this, 0, launchIntent, piFlags);

        PendingIntent? prevIntent = CreateActionIntent(ActionPrevious, 1);
        PendingIntent? playPauseIntent = CreateActionIntent(isPlaying ? ActionPause : ActionPlay, 2);
        PendingIntent? nextIntent = CreateActionIntent(ActionNext, 3);

        var prevAction = new Notification.Action.Builder(
            global::Android.Resource.Drawable.IcMediaPrevious,
            "Предыдущий",
            prevIntent).Build()!;

        var playPauseAction = new Notification.Action.Builder(
            isPlaying ? global::Android.Resource.Drawable.IcMediaPause : global::Android.Resource.Drawable.IcMediaPlay,
            isPlaying ? "Пауза" : "Играть",
            playPauseIntent).Build()!;

        var nextAction = new Notification.Action.Builder(
            global::Android.Resource.Drawable.IcMediaNext,
            "Следующий",
            nextIntent).Build()!;

        var mediaStyle = new Notification.MediaStyle()
            .SetMediaSession(_mediaSession!.SessionToken)!
            .SetShowActionsInCompactView(0, 1, 2)!;

        var builder = new Notification.Builder(this, ChannelId);

        builder
            .SetContentTitle(track.Title)
            .SetContentText(track.Artist)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)
            .SetContentIntent(contentPendingIntent)
            .SetOngoing(isPlaying)
            .SetOnlyAlertOnce(true)
            .SetVisibility(NotificationVisibility.Public)
            .SetStyle(mediaStyle)
            .AddAction(prevAction)
            .AddAction(playPauseAction)
            .AddAction(nextAction);

        if (albumArt is not null)
        {
            builder.SetLargeIcon(albumArt);
        }

        return builder.Build()!;
    }

    private PendingIntent? CreateActionIntent(string action, int requestCode)
    {
        var intent = new Intent(this, typeof(MediaPlaybackForegroundService));
        intent.SetAction(action);
        return PendingIntent.GetService(
            this,
            requestCode,
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    private void StopForegroundAndSelf()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            else
            {
#pragma warning disable CA1422
                StopForeground(true);
#pragma warning restore CA1422
            }
        }
        catch
        {
            // Ignore stop foreground errors
        }

        StopSelf();
    }

    private void EnsureNotificationChannel()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        if (GetSystemService(NotificationService) is not NotificationManager manager)
            return;

        if (manager.GetNotificationChannel(ChannelId) is not null)
            return;

        var channel = new NotificationChannel(ChannelId, "Воспроизведение музыки", NotificationImportance.Low)
        {
            Description = "Управление текущим треком в шторке уведомлений",
            LockscreenVisibility = NotificationVisibility.Public
        };
        channel.SetShowBadge(false);
        manager.CreateNotificationChannel(channel);
    }

    private static Bitmap? LoadAlbumArt(string? coverFilePath)
    {
        if (string.IsNullOrEmpty(coverFilePath) || !File.Exists(coverFilePath))
            return null;

        try
        {
            var options = new BitmapFactory.Options { InSampleSize = 2 };
            return BitmapFactory.DecodeFile(coverFilePath, options);
        }
        catch
        {
            return null;
        }
    }

    private sealed class SessionCallback(MediaPlaybackForegroundService service) : MediaSession.Callback
    {
        public override void OnPlay() => service._coordinator?.RaisePlayRequested();
        public override void OnPause() => service._coordinator?.RaisePauseRequested();
        public override void OnSkipToNext() => service._coordinator?.RaiseNextRequested();
        public override void OnSkipToPrevious() => service._coordinator?.RaisePreviousRequested();
        public override void OnStop()
        {
            service._coordinator?.RaiseStopRequested();
            service.StopForegroundAndSelf();
        }
        public override void OnSeekTo(long pos) =>
            service._coordinator?.RaiseSeekRequested(TimeSpan.FromMilliseconds(pos));
    }
}

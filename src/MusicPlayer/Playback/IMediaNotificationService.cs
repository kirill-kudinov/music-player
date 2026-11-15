using System;
using MusicPlayer.Core.AudioTracks;

namespace MusicPlayer.Playback;

public interface IMediaNotificationService
{
    event EventHandler? PlayRequested;
    event EventHandler? PauseRequested;
    event EventHandler? NextRequested;
    event EventHandler? PreviousRequested;
    event EventHandler<TimeSpan>? SeekRequested;
    event EventHandler? StopRequested;

    void UpdatePlayback(AudioTrack? track, bool isPlaying, TimeSpan position);
    void Clear();
}

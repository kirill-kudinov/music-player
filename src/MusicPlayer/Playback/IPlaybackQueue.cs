using System.Collections.Generic;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Player;

namespace MusicPlayer.Playback;

public interface IPlaybackQueue
{
    AudioTrack? CurrentTrack { get; }
    bool HasNext { get;  }
    bool HasPrevious { get; }
    bool IsShuffleEnabled { get; }
    PlaybackRepeatMode RepeatMode { get; }

    void SetTracks(IReadOnlyCollection<AudioTrack> tracks, AudioTrack current);
    AudioTrack? MoveNext();
    AudioTrack? MovePrevious(); 
    void ToggleShuffle();
    void CycleRepeatMode();
}
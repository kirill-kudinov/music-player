using System;

namespace MusicPlayer.Playback;

public interface IAudioPlayer
{
    bool IsPlaying { get; }
    TimeSpan Position { get;  }
    TimeSpan Duration { get; }

    void Play(Uri source);
    void Resume();
    void Pause();
    void Seek(TimeSpan position);
    void Stop(); 
}
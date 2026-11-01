using System;

namespace MusicPlayer.Core.Domain;

public sealed class Track
{
    public string Artist { get; private set; }
    public string Title { get; private set; }
    public string FilePath { get; private set; }
    public bool IsFavorite { get; private set; }
    public TimeSpan Duration { get; private set; }
    
    
}
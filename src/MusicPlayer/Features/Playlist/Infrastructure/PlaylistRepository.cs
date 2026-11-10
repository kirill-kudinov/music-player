using System.Collections.Generic;

namespace MusicPlayer.Core.Playlist;

public sealed class PlaylistRepository
{
    public HashSet<Playlist> Playlists { get; }
    
    private static HashSet<Playlist> CreatePlaylists()
}
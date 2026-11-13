using System.Collections.Generic;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Playlist;

namespace MusicPlayer.Infrastructure;

public static class PlaylistMapper
{
    public static PlaylistItem ToItem(Playlist playlist) => new()
    {
        Id = playlist.Id,
        Name = playlist.Name
    };

    public static Playlist ToEntity(PlaylistItem playlistItem, List<AudioTrack> audioTracks) => Playlist.Create(
        playlistItem.Id,
        playlistItem.Name,
        audioTracks
    );
}
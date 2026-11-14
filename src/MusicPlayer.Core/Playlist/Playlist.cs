using System.Collections.Generic;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.Playlist;

public sealed class Playlist
{
    public int Id { get; }
    public string Name { get; private set; }
    public List<AudioTrack> Tracks { get; }

    private Playlist(int id, string name, List<AudioTrack> tracks)
    {
        Id = id;
        Name = name;
        Tracks = tracks;
    }

    public static Result<Playlist> Create(string name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? Result.Fail(PlaylistErrors.EmptyName)
            : Result.Ok(new Playlist(0, name, []));
    }

    public static Playlist CreateFromDb(
        int id,
        string name,
        List<AudioTrack> audioTracks
    )
    {
        return new Playlist(id, name, audioTracks);
    }
}
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.Playlist;

public sealed class Playlist
{
    public string Name { get; private set; }
    private List<AudioTrack> Tracks { get; }

    private Playlist(string name, List<AudioTrack> tracks)
    {
        Name = name;
        Tracks = tracks;
    }

    public void Add(AudioTrack audioTrack) => Tracks.Add(audioTrack);

    public bool Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;

        Name = newName;

        return true;
    }

    public static Result<Playlist> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Result.Fail(PlaylistErrors.EmptyName);

        return Result.Ok(new Playlist(name, []));
    }

    public static async Task<Playlist> CreateAsync(DirectoryInfo playlistDirectory)
    {
        var normalizedName = NameNormalizer.ForEntity(playlistDirectory.Name);
        var tracks = await CreateTracksAsync(playlistDirectory.FullName);

        return new Playlist(normalizedName, tracks);
    }

    private static async Task<List<AudioTrack>> CreateTracksAsync(string directoryPath)
    {
        var trackPaths = await File.ReadAllLinesAsync(directoryPath);
        var tracks = new List<AudioTrack>();

        if (trackPaths.Length == 0) return [];

        foreach (var trackPath in trackPaths)
        {
            var createResult = AudioTrack.Create(trackPath);

            if (!createResult.TryGetOk(out var track)) continue;

            tracks.Add(track);
        }

        return tracks;
    }
}
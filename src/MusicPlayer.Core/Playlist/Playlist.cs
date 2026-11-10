using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.Playlist;

public sealed class Playlist
{
    public string Name { get; private set; }
    private HashSet<AudioTrack> Tracks { get; }

    private Playlist(string name, HashSet<AudioTrack> tracks)
    {
        Name = name;
        Tracks = tracks;
    }

    public static Result<Playlist> Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Result.Fail(PlaylistErrors.EmptyName);

        return Result.Ok(new Playlist(name, []));
    }

    public static async Task<Playlist> Create(DirectoryInfo playlistDirectory)
    {
        var normalizedName = NormalizeDirectoryName(playlistDirectory.Name);
        var tracks = await CreateTracks(playlistDirectory.FullName);

        return new Playlist(normalizedName, tracks);
    }

    public void Add(AudioTrack audioTrack) => Tracks.Add(audioTrack);

    public bool Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return false;

        Name = newName;

        return true;
    }

    private static string NormalizeDirectoryName(string directoryName)
    {
        StringBuilder stringBuilder = new();

        var words = directoryName.Split('_');

        for (var i = 0; i < words.Length; i++)
        {
            if (i == words.Length)
            {
                stringBuilder.Append($"{words[i]}");
                continue;
            }

            stringBuilder.Append($"{words[i]} ");
        }

        return stringBuilder.ToString();
    }

    private static async Task<HashSet<AudioTrack>> CreateTracks(string directoryPath)
    {
        var trackPaths = await File.ReadAllLinesAsync(directoryPath);
        var tracks = new HashSet<AudioTrack>();

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
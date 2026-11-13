using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Playlist;
using SQLite;

namespace MusicPlayer.Infrastructure;

public sealed class Database
{
    private SQLiteAsyncConnection? _database;

    private async Task Init()
    {
        if (_database is not null) return;

        _database = new SQLiteAsyncConnection(DatabaseConfiguration.DatabasePath, DatabaseConfiguration.Flags);
        await _database.CreateTablesAsync<AudioTrackItem, PlaylistItem, PlaylistAudioTracksItem>();
    }

    public async Task<bool> AddAudioTrack(AudioTrack audioTrack)
    {
        await Init();
        return MapToBool(await _database!.InsertAsync(AudioTrackMapper.ToItem(audioTrack)));
    }

    public async Task<bool> UpdateAudioTrack(AudioTrack audioTrack)
    {
        await Init();
        return MapToBool(await _database!.UpdateAsync(AudioTrackMapper.ToItem(audioTrack)));
    }

    public async Task<bool> DeleteAudioTrack(AudioTrack audioTrack)
    {
        await Init();
        return MapToBool(await _database!.DeleteAsync(AudioTrackMapper.ToItem(audioTrack)));
    }

    public async Task<AudioTrack?> GetAudioTrack(int id)
    {
        await Init();
        return AudioTrackMapper.ToEntity(await _database!
            .Table<AudioTrackItem>()
            .Where(i => i.Id == id)
            .FirstOrDefaultAsync()
        );
    }

    public async Task<List<AudioTrack>> GetAudioTracks()
    {
        await Init();

        var audioTrackItems = await _database!.Table<AudioTrackItem>().ToListAsync();
        var audioTracks = audioTrackItems.Select(AudioTrackMapper.ToEntity).ToList();

        return audioTracks;
    }

    public async Task<bool> AddPlaylist(Playlist playlist)
    {
        await Init();
        return MapToBool(await _database!.InsertAsync(PlaylistMapper.ToItem(playlist)));
    }

    public async Task<bool> UpdatePlaylist(Playlist playlist)
    {
        await Init();
        return MapToBool(await _database!.UpdateAsync(PlaylistMapper.ToItem(playlist)));
    }

    public async Task<bool> DeletePlaylist(Playlist playlist)
    {
        await Init();
        return MapToBool(await _database!.DeleteAsync(PlaylistMapper.ToItem(playlist)));
    }

    public async Task<List<Playlist>> GetPlaylists()
    {
        await Init();

        var playlistItems = await _database!.Table<PlaylistItem>().ToListAsync();
        var playlists = new List<Playlist>();

        foreach (var playlistItem in playlistItems)
        {
            var connections = await _database!
                .Table<PlaylistAudioTracksItem>()
                .Where(i => i.PlaylistId == playlistItem.Id)
                .ToListAsync();

            var audioTracks = new List<AudioTrack>();

            foreach (var connection in connections)
                audioTracks.Add(AudioTrackMapper.ToEntity(await _database!
                    .Table<AudioTrackItem>()
                    .Where(i => i.Id == connection.AudioTrackId)
                    .FirstAsync()
                ));

            playlists.Add(PlaylistMapper.ToEntity(playlistItem, audioTracks));
        }

        return playlists;
    }

    public async Task<bool> AddAudioTrackToPlaylist(AudioTrack audioTrack, Playlist playlist)
    {
        await Init();
        return MapToBool(await _database!.InsertAsync(new PlaylistAudioTracksItem
        {
            AudioTrackId = audioTrack.Id,
            PlaylistId = playlist.Id
        }));
    }

    private static bool MapToBool(int result)
    {
        return result switch
        {
            1 => true,
            _ => false
        };
    }
}
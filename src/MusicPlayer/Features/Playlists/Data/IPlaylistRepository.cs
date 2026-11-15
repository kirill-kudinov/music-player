using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Playlists;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Playlists.Data;

public sealed class TrackFavoriteChangedEventArgs(Uri contentUri, bool isFavorite) : EventArgs
{
    public Uri ContentUri { get; } = contentUri;
    public bool IsFavorite { get; } = isFavorite;
}

public interface IPlaylistRepository
{
    event EventHandler? PlaylistsChanged;
    event EventHandler<TrackFavoriteChangedEventArgs>? TrackFavoriteChanged;

    Task<Result<IReadOnlyList<Playlist>>> GetAllPlaylistsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<Playlist>> GetPlaylistByIdAsync(
        Guid playlistId,
        CancellationToken cancellationToken = default);

    Task<Result<Playlist>> CreatePlaylistAsync(
        string title,
        Option<string> coverPath,
        CancellationToken cancellationToken = default);

    Task<Result<Playlist>> UpdatePlaylistAsync(
        Playlist playlist,
        string newTitle,
        Option<string> newCoverPath,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> DeletePlaylistAsync(
        Playlist playlist,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> SyncFavoritesWithTracksAsync(
        IReadOnlyList<AudioTrack> tracks,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> SetTrackFavoriteAsync(
        AudioTrack track,
        bool isFavorite,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> AddTrackToPlaylistAsync(
        Guid playlistId,
        AudioTrack track,
        CancellationToken cancellationToken = default);

    Task<Result<int>> AddTracksToPlaylistAsync(
        Guid playlistId,
        IReadOnlyList<AudioTrack> tracks,
        CancellationToken cancellationToken = default);

    Task<Result<int>> UpdateTrackPlaylistsAsync(
        AudioTrack track,
        IReadOnlyList<Guid> playlistIdsToAdd,
        IReadOnlyList<Guid> playlistIdsToRemove,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> RemoveTrackFromPlaylistAsync(
        Guid playlistId,
        Uri contentUri,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> RemoveTrackFromEverywhereAsync(
        Uri contentUri,
        CancellationToken cancellationToken = default);
}

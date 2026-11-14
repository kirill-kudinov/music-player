using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Playlists;
using MusicPlayer.Core.Primitives;
using SQLite;

namespace MusicPlayer.Features.Playlists.Data;

public sealed class SqlitePlaylistRepository(
    PlaylistDatabaseSettings settings,
    PlaylistCoverStorage coverStorage) : IPlaylistRepository, IDisposable
{
    private readonly SemaphoreSlim _initGate = new(1, 1);
    private SQLiteAsyncConnection? _connection;
    private bool _isDisposed;

    public event EventHandler? PlaylistsChanged;

    public async Task<Result<IReadOnlyList<Playlist>>> GetAllPlaylistsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            List<PlaylistEntity> playlistEntities = await db.Table<PlaylistEntity>()
                .OrderByDescending(p => p.IsSystem)
                .ThenBy(p => p.CreatedAtUtcTicks)
                .ToListAsync()
                .ConfigureAwait(false);

            List<PlaylistTrackEntity> links = await db.Table<PlaylistTrackEntity>()
                .OrderBy(pt => pt.SortOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            List<TrackCacheEntity> cachedTracks = await db.Table<TrackCacheEntity>()
                .ToListAsync()
                .ConfigureAwait(false);

            Dictionary<string, AudioTrack> tracksByUri = new(StringComparer.OrdinalIgnoreCase);
            foreach (TrackCacheEntity cached in cachedTracks)
            {
                if (TryRestoreAudioTrack(cached, out AudioTrack? track))
                {
                    tracksByUri[cached.ContentUri] = track!;
                }
            }

            Dictionary<string, List<AudioTrack>> tracksByPlaylistId = new(StringComparer.OrdinalIgnoreCase);
            foreach (PlaylistTrackEntity link in links)
            {
                if (!tracksByUri.TryGetValue(link.TrackContentUri, out AudioTrack? track))
                    continue;

                if (!tracksByPlaylistId.TryGetValue(link.PlaylistId, out List<AudioTrack>? list))
                {
                    list = [];
                    tracksByPlaylistId[link.PlaylistId] = list;
                }

                list.Add(track);
            }

            List<Playlist> result = new(playlistEntities.Count);
            foreach (PlaylistEntity entity in playlistEntities)
            {
                if (!Guid.TryParse(entity.Id, out Guid playlistId))
                    continue;

                Option<string> coverPath = string.IsNullOrWhiteSpace(entity.CoverPath)
                    ? Option.None
                    : Option.Some(entity.CoverPath);

                tracksByPlaylistId.TryGetValue(entity.Id, out List<AudioTrack>? playlistTracks);

                Result<Playlist> restored = Playlist.Restore(
                    playlistId,
                    entity.Title,
                    coverPath,
                    entity.IsSystem,
                    playlistTracks);

                if (restored.TryGetOk(out Playlist? playlist))
                {
                    result.Add(playlist);
                }
            }

            return Result.Ok<IReadOnlyList<Playlist>>(result);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] GetAllPlaylistsAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Playlist>> GetPlaylistByIdAsync(
        Guid playlistId,
        CancellationToken cancellationToken = default)
    {
        if (playlistId == Guid.Empty)
            return Result.Fail(PlaylistErrors.EmptyId);

        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlistId.ToString("N");

            PlaylistEntity? entity = await db.FindAsync<PlaylistEntity>(playlistIdKey).ConfigureAwait(false);
            if (entity is null)
                return Result.Fail(PlaylistErrors.NotFound);

            List<PlaylistTrackEntity> links = await db.Table<PlaylistTrackEntity>()
                .Where(pt => pt.PlaylistId == playlistIdKey)
                .OrderBy(pt => pt.SortOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            List<TrackCacheEntity> cachedTracks = await db.Table<TrackCacheEntity>()
                .ToListAsync()
                .ConfigureAwait(false);

            Dictionary<string, AudioTrack> tracksByUri = new(StringComparer.OrdinalIgnoreCase);
            foreach (TrackCacheEntity cached in cachedTracks)
            {
                if (TryRestoreAudioTrack(cached, out AudioTrack? track))
                {
                    tracksByUri[cached.ContentUri] = track!;
                }
            }

            List<AudioTrack> orderedTracks = new(links.Count);
            foreach (PlaylistTrackEntity link in links)
            {
                if (tracksByUri.TryGetValue(link.TrackContentUri, out AudioTrack? track))
                {
                    orderedTracks.Add(track);
                }
            }

            Option<string> coverPath = string.IsNullOrWhiteSpace(entity.CoverPath)
                ? Option.None
                : Option.Some(entity.CoverPath);

            return Playlist.Restore(
                playlistId,
                entity.Title,
                coverPath,
                entity.IsSystem,
                orderedTracks);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] GetPlaylistByIdAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<int>> AddTracksToPlaylistAsync(
        Guid playlistId,
        IReadOnlyList<AudioTrack> tracks,
        CancellationToken cancellationToken = default)
    {
        if (playlistId == Guid.Empty)
            return Result.Fail(PlaylistErrors.EmptyId);

        if (tracks.Count == 0)
            return Result.Ok(0);

        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlistId.ToString("N");

            PlaylistEntity? playlistEntity = await db.FindAsync<PlaylistEntity>(playlistIdKey).ConfigureAwait(false);
            if (playlistEntity is null)
                return Result.Fail(PlaylistErrors.NotFound);

            List<PlaylistTrackEntity> existingLinks = await db.Table<PlaylistTrackEntity>()
                .Where(pt => pt.PlaylistId == playlistIdKey)
                .ToListAsync()
                .ConfigureAwait(false);

            HashSet<string> existingUris = new(
                existingLinks.Select(l => l.TrackContentUri),
                StringComparer.OrdinalIgnoreCase);

            int nextSortOrder = existingLinks.Count == 0
                ? 0
                : existingLinks.Max(l => l.SortOrder) + 1;

            int addedCount = 0;

            foreach (AudioTrack track in tracks)
            {
                string uri = track.ContentUri.OriginalString;
                if (playlistEntity.IsSystem && !track.IsFavorite)
                {
                    track.ToggleFavorite();
                }

                TrackCacheEntity? existingCache = await db.FindAsync<TrackCacheEntity>(uri).ConfigureAwait(false);
                bool isFavorite = playlistEntity.IsSystem || track.IsFavorite || (existingCache?.IsFavorite ?? false);
                if (isFavorite && !track.IsFavorite)
                {
                    track.ToggleFavorite();
                }

                await UpsertTrackCacheAsync(db, track, isFavorite).ConfigureAwait(false);

                if (existingUris.Contains(uri))
                    continue;

                await db.InsertAsync(new PlaylistTrackEntity
                {
                    PlaylistId = playlistIdKey,
                    TrackContentUri = uri,
                    SortOrder = nextSortOrder++
                }).ConfigureAwait(false);

                existingUris.Add(uri);
                addedCount++;
            }

            if (addedCount > 0)
            {
                PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            }

            return Result.Ok(addedCount);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] AddTracksToPlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Unit>> RemoveTrackFromPlaylistAsync(
        Guid playlistId,
        Uri contentUri,
        CancellationToken cancellationToken = default)
    {
        if (playlistId == Guid.Empty)
            return Result.Fail(PlaylistErrors.EmptyId);

        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlistId.ToString("N");
            string uri = contentUri.OriginalString;

            PlaylistEntity? playlistEntity = await db.FindAsync<PlaylistEntity>(playlistIdKey).ConfigureAwait(false);
            if (playlistEntity is null)
                return Result.Fail(PlaylistErrors.NotFound);

            await db.ExecuteAsync(
                "DELETE FROM playlist_tracks WHERE PlaylistId = ? AND TrackContentUri = ?",
                playlistIdKey,
                uri).ConfigureAwait(false);

            if (playlistEntity.IsSystem)
            {
                await db.ExecuteAsync(
                    "UPDATE track_cache SET IsFavorite = 0 WHERE ContentUri = ?",
                    uri).ConfigureAwait(false);
            }

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] RemoveTrackFromPlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Playlist>> CreatePlaylistAsync(
        string title,
        Option<string> coverPath,
        CancellationToken cancellationToken = default)
    {
        Result<Playlist> createResult = Playlist.Create(title, coverPath);
        if (!createResult.TryGetOk(out Playlist? playlist))
            return Result.Fail(createResult.Error);

        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            var entity = new PlaylistEntity
            {
                Id = playlist.Id.ToString("N"),
                Title = playlist.Title,
                CoverPath = playlist.CoverPath.TryGetSome(out string? path) ? path : null,
                IsSystem = false,
                CreatedAtUtcTicks = DateTime.UtcNow.Ticks
            };

            await db.InsertAsync(entity).ConfigureAwait(false);
            PlaylistsChanged?.Invoke(this, EventArgs.Empty);

            return Result.Ok(playlist);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] CreatePlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Playlist>> UpdatePlaylistAsync(
        Playlist playlist,
        string newTitle,
        Option<string> newCoverPath,
        CancellationToken cancellationToken = default)
    {
        string previousTitle = playlist.Title;
        Option<string> previousCoverPath = playlist.CoverPath;

        Result<Unit> renameResult = playlist.Rename(newTitle);
        if (renameResult.IsError)
            return Result.Fail(renameResult.Error);

        Result<Unit> coverResult = playlist.UpdateCover(newCoverPath);
        if (coverResult.IsError)
        {
            _ = playlist.Rename(previousTitle);
            return Result.Fail(coverResult.Error);
        }

        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            string playlistIdKey = playlist.Id.ToString("N");
            PlaylistEntity? existing = await db.FindAsync<PlaylistEntity>(playlistIdKey).ConfigureAwait(false);
            if (existing is null)
            {
                _ = playlist.Rename(previousTitle);
                _ = playlist.UpdateCover(previousCoverPath);
                return Result.Fail(PlaylistErrors.NotFound);
            }

            existing.Title = playlist.Title;
            existing.CoverPath = playlist.CoverPath.TryGetSome(out string? updatedPath) ? updatedPath : null;

            await db.UpdateAsync(existing).ConfigureAwait(false);

            if (previousCoverPath.TryGetSome(out string? oldPath) &&
                !string.Equals(oldPath, existing.CoverPath, StringComparison.OrdinalIgnoreCase))
            {
                coverStorage.DeleteCoverIfExists(oldPath);
            }

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok(playlist);
        }
        catch (Exception ex)
        {
            _ = playlist.Rename(previousTitle);
            _ = playlist.UpdateCover(previousCoverPath);
            Debug.WriteLine($"[SqlitePlaylistRepository] UpdatePlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Unit>> DeletePlaylistAsync(
        Playlist playlist,
        CancellationToken cancellationToken = default)
    {
        Result<Unit> canDeleteResult = playlist.EnsureCanBeDeleted();
        if (canDeleteResult.IsError)
            return Result.Fail(canDeleteResult.Error);

        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlist.Id.ToString("N");

            await db.ExecuteAsync(
                "DELETE FROM playlist_tracks WHERE PlaylistId = ?",
                playlistIdKey).ConfigureAwait(false);

            int deletedCount = await db.DeleteAsync<PlaylistEntity>(playlistIdKey).ConfigureAwait(false);
            if (deletedCount == 0)
                return Result.Fail(PlaylistErrors.NotFound);

            if (playlist.CoverPath.TryGetSome(out string? coverPath))
            {
                coverStorage.DeleteCoverIfExists(coverPath);
            }

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] DeletePlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Unit>> SyncFavoritesWithTracksAsync(
        IReadOnlyList<AudioTrack> tracks,
        CancellationToken cancellationToken = default)
    {
        if (tracks.Count == 0)
            return Result.Ok();

        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            PlaylistEntity favoritesEntity = await GetOrCreateFavoritesEntityAsync(db).ConfigureAwait(false);

            List<PlaylistTrackEntity> favoriteLinks = await db.Table<PlaylistTrackEntity>()
                .Where(pt => pt.PlaylistId == favoritesEntity.Id)
                .OrderBy(pt => pt.SortOrder)
                .ToListAsync()
                .ConfigureAwait(false);

            HashSet<string> favoriteUris = new(
                favoriteLinks.Select(l => l.TrackContentUri),
                StringComparer.OrdinalIgnoreCase);

            int nextSortOrder = favoriteLinks.Count == 0
                ? 0
                : favoriteLinks.Max(l => l.SortOrder) + 1;

            bool changed = false;

            foreach (AudioTrack track in tracks)
            {
                string uri = track.ContentUri.OriginalString;
                bool isInDbFavorites = favoriteUris.Contains(uri);

                if (isInDbFavorites && !track.IsFavorite)
                {
                    track.ToggleFavorite();
                }
                else if (!isInDbFavorites && track.IsFavorite)
                {
                    await db.InsertAsync(new PlaylistTrackEntity
                    {
                        PlaylistId = favoritesEntity.Id,
                        TrackContentUri = uri,
                        SortOrder = nextSortOrder++
                    }).ConfigureAwait(false);

                    favoriteUris.Add(uri);
                    isInDbFavorites = true;
                    changed = true;
                }

                await UpsertTrackCacheAsync(db, track, isInDbFavorites).ConfigureAwait(false);
            }

            if (changed)
            {
                PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] SyncFavoritesWithTracksAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Unit>> SetTrackFavoriteAsync(
        AudioTrack track,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            PlaylistEntity favoritesEntity = await GetOrCreateFavoritesEntityAsync(db).ConfigureAwait(false);

            string uri = track.ContentUri.OriginalString;
            await UpsertTrackCacheAsync(db, track, isFavorite).ConfigureAwait(false);

            PlaylistTrackEntity? existingLink = await db.Table<PlaylistTrackEntity>()
                .Where(pt => pt.PlaylistId == favoritesEntity.Id && pt.TrackContentUri == uri)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (isFavorite && existingLink is null)
            {
                int nextSortOrder = await GetNextSortOrderAsync(db, favoritesEntity.Id).ConfigureAwait(false);
                await db.InsertAsync(new PlaylistTrackEntity
                {
                    PlaylistId = favoritesEntity.Id,
                    TrackContentUri = uri,
                    SortOrder = nextSortOrder
                }).ConfigureAwait(false);
            }
            else if (!isFavorite && existingLink is not null)
            {
                await db.DeleteAsync(existingLink).ConfigureAwait(false);
            }

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] SetTrackFavoriteAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Unit>> AddTrackToPlaylistAsync(
        Guid playlistId,
        AudioTrack track,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlistId.ToString("N");

            PlaylistEntity? playlistEntity = await db.FindAsync<PlaylistEntity>(playlistIdKey).ConfigureAwait(false);
            if (playlistEntity is null)
                return Result.Fail(PlaylistErrors.NotFound);

            if (playlistEntity.IsSystem)
            {
                if (!track.IsFavorite)
                {
                    track.ToggleFavorite();
                }

                return await SetTrackFavoriteAsync(track, isFavorite: true, cancellationToken).ConfigureAwait(false);
            }

            string uri = track.ContentUri.OriginalString;
            TrackCacheEntity? existingCache = await db.FindAsync<TrackCacheEntity>(uri).ConfigureAwait(false);
            bool isFavorite = track.IsFavorite || (existingCache?.IsFavorite ?? false);
            await UpsertTrackCacheAsync(db, track, isFavorite).ConfigureAwait(false);

            PlaylistTrackEntity? existingLink = await db.Table<PlaylistTrackEntity>()
                .Where(pt => pt.PlaylistId == playlistIdKey && pt.TrackContentUri == uri)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (existingLink is not null)
                return Result.Fail(PlaylistErrors.TrackAlreadyInPlaylist);

            int nextSortOrder = await GetNextSortOrderAsync(db, playlistIdKey).ConfigureAwait(false);
            await db.InsertAsync(new PlaylistTrackEntity
            {
                PlaylistId = playlistIdKey,
                TrackContentUri = uri,
                SortOrder = nextSortOrder
            }).ConfigureAwait(false);

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] AddTrackToPlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    public async Task<Result<Unit>> RemoveTrackFromEverywhereAsync(
        Uri contentUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string uri = contentUri.OriginalString;

            await db.ExecuteAsync(
                "DELETE FROM playlist_tracks WHERE TrackContentUri = ?",
                uri).ConfigureAwait(false);

            await db.DeleteAsync<TrackCacheEntity>(uri).ConfigureAwait(false);

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] RemoveTrackFromEverywhereAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
    }

    private async Task<SQLiteAsyncConnection> EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            return _connection;

        await _initGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is not null)
                return _connection;

            var connection = new SQLiteAsyncConnection(settings.DatabasePath, settings.OpenFlags);
            await connection.CreateTableAsync<PlaylistEntity>().ConfigureAwait(false);
            await connection.CreateTableAsync<TrackCacheEntity>().ConfigureAwait(false);
            await connection.CreateTableAsync<PlaylistTrackEntity>().ConfigureAwait(false);

            await GetOrCreateFavoritesEntityAsync(connection).ConfigureAwait(false);

            _connection = connection;
            return _connection;
        }
        finally
        {
            _initGate.Release();
        }
    }

    private static async Task<PlaylistEntity> GetOrCreateFavoritesEntityAsync(SQLiteAsyncConnection db)
    {
        PlaylistEntity? existing = await db.Table<PlaylistEntity>()
            .Where(p => p.IsSystem)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is not null)
            return existing;

        Playlist favorites = Playlist.CreateFavorites();
        var entity = new PlaylistEntity
        {
            Id = favorites.Id.ToString("N"),
            Title = favorites.Title,
            CoverPath = null,
            IsSystem = true,
            CreatedAtUtcTicks = DateTime.UtcNow.Ticks
        };

        await db.InsertAsync(entity).ConfigureAwait(false);
        return entity;
    }

    private static async Task UpsertTrackCacheAsync(
        SQLiteAsyncConnection db,
        AudioTrack track,
        bool isFavorite)
    {
        var entity = new TrackCacheEntity
        {
            ContentUri = track.ContentUri.OriginalString,
            Title = track.Title ?? string.Empty,
            Artist = track.Artist ?? string.Empty,
            Format = (int)track.Format,
            DurationTicks = track.Duration.Ticks,
            IsFavorite = isFavorite
        };

        await db.InsertOrReplaceAsync(entity).ConfigureAwait(false);
    }

    private static async Task<int> GetNextSortOrderAsync(SQLiteAsyncConnection db, string playlistId)
    {
        List<PlaylistTrackEntity> links = await db.Table<PlaylistTrackEntity>()
            .Where(pt => pt.PlaylistId == playlistId)
            .ToListAsync()
            .ConfigureAwait(false);

        return links.Count == 0 ? 0 : links.Max(l => l.SortOrder) + 1;
    }

    private static bool TryRestoreAudioTrack(TrackCacheEntity cached, out AudioTrack? track)
    {
        track = null;
        if (!Uri.TryCreate(cached.ContentUri, UriKind.RelativeOrAbsolute, out Uri? uri))
            return false;

        TimeSpan duration = TimeSpan.FromTicks(cached.DurationTicks);
        if (duration <= TimeSpan.Zero)
            return false;

        Result<AudioTrack> result = AudioTrack.Create(
            uri,
            (AudioFormat)cached.Format,
            cached.Artist,
            cached.Title,
            duration,
            cached.IsFavorite);

        return result.TryGetOk(out track);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _initGate.Dispose();
    }
}

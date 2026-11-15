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
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private SQLiteAsyncConnection? _connection;
    private bool _isDisposed;

    public event EventHandler? PlaylistsChanged;
    public event EventHandler<TrackFavoriteChangedEventArgs>? TrackFavoriteChanged;

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

            Dictionary<string, AudioTrack> tracksByUri = BuildTrackLookup(cachedTracks);
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

            List<AudioTrack> orderedTracks = [];
            if (links.Count > 0)
            {
                List<TrackCacheEntity> cachedTracks = await db.Table<TrackCacheEntity>()
                    .ToListAsync()
                    .ConfigureAwait(false);

                Dictionary<string, AudioTrack> tracksByUri = BuildTrackLookup(cachedTracks);
                orderedTracks.Capacity = links.Count;

                foreach (PlaylistTrackEntity link in links)
                {
                    if (tracksByUri.TryGetValue(link.TrackContentUri, out AudioTrack? track))
                    {
                        orderedTracks.Add(track);
                    }
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

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlistId.ToString("N");

            bool playlistMissing = false;
            bool isSystemPlaylist = false;
            int addedCount = 0;
            List<Uri> newlyFavoritedUris = [];

            await db.RunInTransactionAsync(conn =>
            {
                PlaylistEntity? playlistEntity = conn.Find<PlaylistEntity>(playlistIdKey);
                if (playlistEntity is null)
                {
                    playlistMissing = true;
                    return;
                }

                isSystemPlaylist = playlistEntity.IsSystem;

                List<PlaylistTrackEntity> existingLinks = conn.Table<PlaylistTrackEntity>()
                    .Where(pt => pt.PlaylistId == playlistIdKey)
                    .ToList();

                HashSet<string> existingUris = new(
                    existingLinks.Select(l => l.TrackContentUri),
                    StringComparer.OrdinalIgnoreCase);

                int nextSortOrder = existingLinks.Count == 0
                    ? 0
                    : existingLinks.Max(l => l.SortOrder) + 1;

                foreach (AudioTrack track in tracks)
                {
                    string uri = track.ContentUri.OriginalString;
                    TrackCacheEntity? existingCache = conn.Find<TrackCacheEntity>(uri);
                    bool isFavorite = isSystemPlaylist || (existingCache?.IsFavorite ?? track.IsFavorite);

                    UpsertTrackCacheSync(conn, track, isFavorite);

                    if (isSystemPlaylist)
                    {
                        track.SetFavorite(true);
                    }

                    if (!existingUris.Add(uri))
                        continue;

                    conn.Insert(new PlaylistTrackEntity
                    {
                        PlaylistId = playlistIdKey,
                        TrackContentUri = uri,
                        SortOrder = nextSortOrder++
                    });

                    addedCount++;
                    if (isSystemPlaylist)
                    {
                        newlyFavoritedUris.Add(track.ContentUri);
                    }
                }
            }).ConfigureAwait(false);

            if (playlistMissing)
                return Result.Fail(PlaylistErrors.NotFound);

            if (addedCount > 0)
            {
                foreach (Uri uri in newlyFavoritedUris)
                {
                    TrackFavoriteChanged?.Invoke(this, new TrackFavoriteChangedEventArgs(uri, isFavorite: true));
                }

                PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            }

            return Result.Ok(addedCount);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] AddTracksToPlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Result<int>> UpdateTrackPlaylistsAsync(
        AudioTrack track,
        IReadOnlyList<Guid> playlistIdsToAdd,
        IReadOnlyList<Guid> playlistIdsToRemove,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (playlistIdsToAdd.Count == 0 && playlistIdsToRemove.Count == 0)
            return Result.Ok(0);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string uri = track.ContentUri.OriginalString;

            int changesCount = 0;
            bool? updatedFavoriteState = null;

            await db.RunInTransactionAsync(conn =>
            {
                TrackCacheEntity? existingCache = conn.Find<TrackCacheEntity>(uri);
                bool currentFavorite = existingCache?.IsFavorite ?? track.IsFavorite;

                foreach (Guid playlistId in playlistIdsToAdd)
                {
                    if (playlistId == Guid.Empty)
                        continue;

                    string playlistIdKey = playlistId.ToString("N");
                    PlaylistEntity? playlistEntity = conn.Find<PlaylistEntity>(playlistIdKey);
                    if (playlistEntity is null)
                        continue;

                    if (playlistEntity.IsSystem)
                    {
                        currentFavorite = true;
                        updatedFavoriteState = true;
                    }

                    PlaylistTrackEntity? existingLink = conn.Table<PlaylistTrackEntity>()
                        .Where(pt => pt.PlaylistId == playlistIdKey && pt.TrackContentUri == uri).FirstOrDefault();

                    if (existingLink is not null)
                        continue;

                    int nextSortOrder = GetNextSortOrderSync(conn, playlistIdKey);
                    conn.Insert(new PlaylistTrackEntity
                    {
                        PlaylistId = playlistIdKey,
                        TrackContentUri = uri,
                        SortOrder = nextSortOrder
                    });

                    changesCount++;
                }

                foreach (Guid playlistId in playlistIdsToRemove)
                {
                    if (playlistId == Guid.Empty)
                        continue;

                    string playlistIdKey = playlistId.ToString("N");
                    PlaylistEntity? playlistEntity = conn.Find<PlaylistEntity>(playlistIdKey);
                    if (playlistEntity is null)
                        continue;

                    int deleted = conn.Execute(
                        "DELETE FROM playlist_tracks WHERE PlaylistId = ? AND TrackContentUri = ?",
                        playlistIdKey,
                        uri);

                    if (playlistEntity.IsSystem && deleted > 0)
                    {
                        currentFavorite = false;
                        updatedFavoriteState = false;
                    }

                    if (deleted > 0)
                    {
                        changesCount++;
                    }
                }

                UpsertTrackCacheSync(conn, track, currentFavorite);
            }).ConfigureAwait(false);

            if (updatedFavoriteState.HasValue)
            {
                track.SetFavorite(updatedFavoriteState.Value);
                TrackFavoriteChanged?.Invoke(
                    this,
                    new TrackFavoriteChangedEventArgs(track.ContentUri, updatedFavoriteState.Value));
            }

            if (changesCount > 0)
            {
                PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            }

            return Result.Ok(changesCount);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] UpdateTrackPlaylistsAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Result<Unit>> RemoveTrackFromPlaylistAsync(
        Guid playlistId,
        Uri contentUri,
        CancellationToken cancellationToken = default)
    {
        if (playlistId == Guid.Empty)
            return Result.Fail(PlaylistErrors.EmptyId);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlistId.ToString("N");
            string uri = contentUri.OriginalString;

            bool playlistMissing = false;
            bool removedFromFavorites = false;

            await db.RunInTransactionAsync(conn =>
            {
                PlaylistEntity? playlistEntity = conn.Find<PlaylistEntity>(playlistIdKey);
                if (playlistEntity is null)
                {
                    playlistMissing = true;
                    return;
                }

                conn.Execute(
                    "DELETE FROM playlist_tracks WHERE PlaylistId = ? AND TrackContentUri = ?",
                    playlistIdKey,
                    uri);

                if (playlistEntity.IsSystem)
                {
                    conn.Execute(
                        "UPDATE track_cache SET IsFavorite = 0 WHERE ContentUri = ?",
                        uri);
                    removedFromFavorites = true;
                }
            }).ConfigureAwait(false);

            if (playlistMissing)
                return Result.Fail(PlaylistErrors.NotFound);

            if (removedFromFavorites)
            {
                TrackFavoriteChanged?.Invoke(this, new TrackFavoriteChangedEventArgs(contentUri, isFavorite: false));
            }

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] RemoveTrackFromPlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
        finally
        {
            _writeGate.Release();
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

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        finally
        {
            _writeGate.Release();
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

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Result<Unit>> DeletePlaylistAsync(
        Playlist playlist,
        CancellationToken cancellationToken = default)
    {
        Result<Unit> canDeleteResult = playlist.EnsureCanBeDeleted();
        if (canDeleteResult.IsError)
            return Result.Fail(canDeleteResult.Error);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlist.Id.ToString("N");

            int deletedCount = 0;
            await db.RunInTransactionAsync(conn =>
            {
                conn.Execute(
                    "DELETE FROM playlist_tracks WHERE PlaylistId = ?",
                    playlistIdKey);

                deletedCount = conn.Delete<PlaylistEntity>(playlistIdKey);
            }).ConfigureAwait(false);

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
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Синхронизирует состояние IsFavorite загруженных из памяти треков с базой данных.
    /// База данных является единственным источником истины для избранного.
    /// </summary>
    public async Task<Result<Unit>> SyncFavoritesWithTracksAsync(
        IReadOnlyList<AudioTrack> tracks,
        CancellationToken cancellationToken = default)
    {
        if (tracks.Count == 0)
            return Result.Ok();

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

            await db.RunInTransactionAsync(conn =>
            {
                PlaylistEntity favoritesEntity = GetOrCreateFavoritesEntitySync(conn);

                List<PlaylistTrackEntity> favoriteLinks = conn.Table<PlaylistTrackEntity>()
                    .Where(pt => pt.PlaylistId == favoritesEntity.Id)
                    .ToList();

                HashSet<string> favoriteUris = new(
                    favoriteLinks.Select(l => l.TrackContentUri),
                    StringComparer.OrdinalIgnoreCase);

                foreach (AudioTrack track in tracks)
                {
                    string uri = track.ContentUri.OriginalString;
                    bool isInDbFavorites = favoriteUris.Contains(uri);

                    if (track.IsFavorite != isInDbFavorites)
                    {
                        track.SetFavorite(isInDbFavorites);
                    }

                    UpsertTrackCacheSync(conn, track, isInDbFavorites);
                }
            }).ConfigureAwait(false);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] SyncFavoritesWithTracksAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Result<Unit>> SetTrackFavoriteAsync(
        AudioTrack track,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string uri = track.ContentUri.OriginalString;

            await db.RunInTransactionAsync(conn =>
            {
                PlaylistEntity favoritesEntity = GetOrCreateFavoritesEntitySync(conn);
                UpsertTrackCacheSync(conn, track, isFavorite);

                PlaylistTrackEntity? existingLink = conn.Table<PlaylistTrackEntity>()
                    .Where(pt => pt.PlaylistId == favoritesEntity.Id && pt.TrackContentUri == uri).FirstOrDefault();

                if (isFavorite && existingLink is null)
                {
                    int nextSortOrder = GetNextSortOrderSync(conn, favoritesEntity.Id);
                    conn.Insert(new PlaylistTrackEntity
                    {
                        PlaylistId = favoritesEntity.Id,
                        TrackContentUri = uri,
                        SortOrder = nextSortOrder
                    });
                }
                else if (!isFavorite && existingLink is not null)
                {
                    conn.Delete(existingLink);
                }
            }).ConfigureAwait(false);

            track.SetFavorite(isFavorite);
            TrackFavoriteChanged?.Invoke(this, new TrackFavoriteChangedEventArgs(track.ContentUri, isFavorite));
            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] SetTrackFavoriteAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Result<Unit>> AddTrackToPlaylistAsync(
        Guid playlistId,
        AudioTrack track,
        CancellationToken cancellationToken = default)
    {
        if (playlistId == Guid.Empty)
            return Result.Fail(PlaylistErrors.EmptyId);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string playlistIdKey = playlistId.ToString("N");
            string uri = track.ContentUri.OriginalString;

            bool playlistMissing = false;
            bool alreadyInPlaylist = false;
            bool isSystemPlaylist = false;

            await db.RunInTransactionAsync(conn =>
            {
                PlaylistEntity? playlistEntity = conn.Find<PlaylistEntity>(playlistIdKey);
                if (playlistEntity is null)
                {
                    playlistMissing = true;
                    return;
                }

                isSystemPlaylist = playlistEntity.IsSystem;

                TrackCacheEntity? existingCache = conn.Find<TrackCacheEntity>(uri);
                bool isFavorite = isSystemPlaylist || (existingCache?.IsFavorite ?? track.IsFavorite);
                UpsertTrackCacheSync(conn, track, isFavorite);

                PlaylistTrackEntity? existingLink = conn.Table<PlaylistTrackEntity>()
                    .Where(pt => pt.PlaylistId == playlistIdKey && pt.TrackContentUri == uri).FirstOrDefault();

                if (existingLink is not null)
                {
                    alreadyInPlaylist = true;
                    return;
                }

                int nextSortOrder = GetNextSortOrderSync(conn, playlistIdKey);
                conn.Insert(new PlaylistTrackEntity
                {
                    PlaylistId = playlistIdKey,
                    TrackContentUri = uri,
                    SortOrder = nextSortOrder
                });
            }).ConfigureAwait(false);

            if (playlistMissing)
                return Result.Fail(PlaylistErrors.NotFound);

            if (isSystemPlaylist)
            {
                track.SetFavorite(true);
                TrackFavoriteChanged?.Invoke(this, new TrackFavoriteChangedEventArgs(track.ContentUri, isFavorite: true));
            }

            if (alreadyInPlaylist)
            {
                if (isSystemPlaylist)
                {
                    PlaylistsChanged?.Invoke(this, EventArgs.Empty);
                    return Result.Ok();
                }

                return Result.Fail(PlaylistErrors.TrackAlreadyInPlaylist);
            }

            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] AddTrackToPlaylistAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<Result<Unit>> RemoveTrackFromEverywhereAsync(
        Uri contentUri,
        CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SQLiteAsyncConnection db = await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            string uri = contentUri.OriginalString;

            await db.RunInTransactionAsync(conn =>
            {
                conn.Execute(
                    "DELETE FROM playlist_tracks WHERE TrackContentUri = ?",
                    uri);

                conn.Delete<TrackCacheEntity>(uri);
            }).ConfigureAwait(false);

            TrackFavoriteChanged?.Invoke(this, new TrackFavoriteChangedEventArgs(contentUri, isFavorite: false));
            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SqlitePlaylistRepository] RemoveTrackFromEverywhereAsync failed: {ex.Message}");
            return Result.Fail(PlaylistStorageErrors.DatabaseOperationFailed);
        }
        finally
        {
            _writeGate.Release();
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

            await connection.RunInTransactionAsync(conn => GetOrCreateFavoritesEntitySync(conn)).ConfigureAwait(false);

            _connection = connection;
            return _connection;
        }
        finally
        {
            _initGate.Release();
        }
    }

    private static PlaylistEntity GetOrCreateFavoritesEntitySync(SQLiteConnection conn)
    {
        PlaylistEntity? existing = conn.Table<PlaylistEntity>()
            .Where(p => p.IsSystem).FirstOrDefault();

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

        conn.Insert(entity);
        return entity;
    }

    private static void UpsertTrackCacheSync(
        SQLiteConnection conn,
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

        conn.InsertOrReplace(entity);
    }

    private static int GetNextSortOrderSync(SQLiteConnection conn, string playlistId)
    {
        List<PlaylistTrackEntity> links = conn.Table<PlaylistTrackEntity>()
            .Where(pt => pt.PlaylistId == playlistId)
            .ToList();

        return links.Count == 0 ? 0 : links.Max(l => l.SortOrder) + 1;
    }

    private static Dictionary<string, AudioTrack> BuildTrackLookup(IEnumerable<TrackCacheEntity> cachedTracks)
    {
        Dictionary<string, AudioTrack> tracksByUri = new(StringComparer.OrdinalIgnoreCase);
        foreach (TrackCacheEntity cached in cachedTracks)
        {
            if (TryRestoreAudioTrack(cached, out AudioTrack? track))
            {
                tracksByUri[cached.ContentUri] = track!;
            }
        }

        return tracksByUri;
    }

    private static bool TryRestoreAudioTrack(TrackCacheEntity cached, out AudioTrack? track)
    {
        track = null;
        if (!Uri.TryCreate(cached.ContentUri, UriKind.Absolute, out Uri? uri) &&
            !Uri.TryCreate(cached.ContentUri, UriKind.RelativeOrAbsolute, out uri))
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
        _writeGate.Dispose();
    }
}

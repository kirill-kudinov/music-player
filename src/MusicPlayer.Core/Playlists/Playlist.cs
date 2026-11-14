using System;
using System.Collections.Generic;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.Playlists;

public sealed class Playlist
{
    public const int MaxTitleLength = 100;
    public const string FavoritesTitle = "Избранное";

    private readonly List<AudioTrack> _tracks = [];

    public Guid Id { get; }
    public string Title { get; private set; }
    public Option<string> CoverPath { get; private set; }
    public bool IsSystem { get; }
    public IReadOnlyList<AudioTrack> Tracks => _tracks;
    public int TrackCount => _tracks.Count;

    private Playlist(
        Guid id,
        string title,
        Option<string> coverPath,
        bool isSystem,
        IEnumerable<AudioTrack>? tracks)
    {
        Id = id;
        Title = title;
        CoverPath = coverPath;
        IsSystem = isSystem;

        if (tracks is not null)
        {
            foreach (AudioTrack track in tracks)
            {
                if (!ContainsTrack(track.ContentUri))
                {
                    _tracks.Add(track);
                }
            }
        }
    }

    public static Result<Playlist> Create(string title, Option<string> coverPath = default)
    {
        var titleResult = ValidateTitle(title);
        if (!titleResult.TryGetOk(out string? normalizedTitle))
            return Result.Fail(titleResult.Error);

        var coverResult = ValidateCoverPath(coverPath);
        if (coverResult.IsError)
            return Result.Fail(coverResult.Error);

        var playlist = new Playlist(
            Guid.NewGuid(),
            normalizedTitle,
            coverPath,
            isSystem: false,
            tracks: null);

        return Result.Ok(playlist);
    }

    public static Playlist CreateFavorites(Guid? id = null, IEnumerable<AudioTrack>? tracks = null)
    {
        Guid resolvedId = id is { } existing && existing != Guid.Empty
            ? existing
            : Guid.NewGuid();

        return new Playlist(
            resolvedId,
            FavoritesTitle,
            Option.None,
            isSystem: true,
            tracks);
    }

    public static Result<Playlist> Restore(
        Guid id,
        string title,
        Option<string> coverPath,
        bool isSystem,
        IEnumerable<AudioTrack>? tracks = null)
    {
        if (id == Guid.Empty)
            return Result.Fail(PlaylistErrors.EmptyId);

        if (isSystem)
            return Result.Ok(CreateFavorites(id, tracks));

        var titleResult = ValidateTitle(title);
        if (!titleResult.TryGetOk(out string? normalizedTitle))
            return Result.Fail(titleResult.Error);

        var coverResult = ValidateCoverPath(coverPath);
        if (coverResult.IsError)
            return Result.Fail(coverResult.Error);

        return Result.Ok(new Playlist(id, normalizedTitle, coverPath, isSystem: false, tracks));
    }

    public Result<Unit> Rename(string newTitle)
    {
        if (IsSystem)
            return Result.Fail(PlaylistErrors.SystemPlaylistImmutable);

        var titleResult = ValidateTitle(newTitle);
        if (!titleResult.TryGetOk(out string? normalizedTitle))
            return Result.Fail(titleResult.Error);

        Title = normalizedTitle;
        return Result.Ok();
    }

    public Result<Unit> UpdateCover(Option<string> newCoverPath)
    {
        if (IsSystem)
            return Result.Fail(PlaylistErrors.SystemPlaylistImmutable);

        var coverResult = ValidateCoverPath(newCoverPath);
        if (coverResult.IsError)
            return Result.Fail(coverResult.Error);

        CoverPath = newCoverPath;
        return Result.Ok();
    }

    public Result<Unit> EnsureCanBeDeleted() =>
        IsSystem
            ? Result.Fail(PlaylistErrors.CannotDeleteSystemPlaylist)
            : Result.Ok();

    public Result<Unit> AddTrack(AudioTrack track)
    {
        if (ContainsTrack(track.ContentUri))
            return Result.Fail(PlaylistErrors.TrackAlreadyInPlaylist);

        _tracks.Add(track);
        return Result.Ok();
    }

    public Result<Unit> RemoveTrack(Uri contentUri)
    {
        int index = FindTrackIndex(contentUri);
        if (index < 0)
            return Result.Fail(PlaylistErrors.TrackNotInPlaylist);

        _tracks.RemoveAt(index);
        return Result.Ok();
    }

    public bool ContainsTrack(Uri contentUri) => FindTrackIndex(contentUri) >= 0;

    public void ReplaceTracks(IEnumerable<AudioTrack> tracks)
    {
        _tracks.Clear();
        foreach (AudioTrack track in tracks)
        {
            if (!ContainsTrack(track.ContentUri))
            {
                _tracks.Add(track);
            }
        }
    }

    private int FindTrackIndex(Uri contentUri)
    {
        for (int i = 0; i < _tracks.Count; i++)
        {
            if (_tracks[i].ContentUri == contentUri)
                return i;
        }

        return -1;
    }

    private static Result<string> ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Fail(PlaylistErrors.EmptyTitle);

        string trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
            return Result.Fail(PlaylistErrors.TitleTooLong);

        return Result.Ok(trimmed);
    }

    private static Result<Unit> ValidateCoverPath(Option<string> coverPath)
    {
        if (coverPath.TryGetSome(out string? path) && string.IsNullOrWhiteSpace(path))
            return Result.Fail(PlaylistErrors.EmptyCoverPath);

        return Result.Ok();
    }
}

using System;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.AudioTracks;

public sealed class AudioTrack
{
    public int Id { get; }

    public string? Artist
    {
        get;
        private set
        {
            if (string.IsNullOrWhiteSpace(value))
                field = UnknowArtist;

            field = value;
        }
    }

    public string? Title
    {
        get;
        private set
        {
            if (string.IsNullOrWhiteSpace(value))
                field = Untitled;
        }
    }

    public bool IsFavorite { get; private set; }
    public TimeSpan Duration { get; private set; }

    private const string UnknowArtist = "Неизвестный исполнитель";
    private const string Untitled = "Без названия";

    private AudioTrack(int id, string artist, string title, TimeSpan duration)
    {
        Id = id;
        Artist = artist;
        Title = title;
        Duration = duration;
    }

    public void ToggleFavorite() => IsFavorite = !IsFavorite;

    public Result<Unit> RenameTitle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail(AudioTrackErrors.InvalidRenaming);

        Title = name;
        return Result.Ok();
    }

    public Result<Unit> RenameArtist(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail(AudioTrackErrors.InvalidRenaming);

        Artist = name;
        return Result.Ok();
    }


    public static Result<AudioTrack> Create(
        int id,
        string artist,
        string title,
        bool isFavorite,
        TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
            return Result.Fail(AudioTrackErrors.NegativeOrZeroDuration);

        return Result.Ok(new AudioTrack(id, artist, title, duration));
    }
}
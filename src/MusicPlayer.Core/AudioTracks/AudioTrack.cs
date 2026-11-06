using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.AudioTracks;

public partial class AudioTrack : ObservableObject
{
    public long Id { get; }
    public AudioFormat Format { get; }
    public Uri ContentUri { get; }

    public string? Title
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? Untitled : value.Trim();
    }

    public string? Artist
    {
        get;
        private set => field = string.IsNullOrWhiteSpace(value) ? UnknowArtist : value.Trim();
    }


    [ObservableProperty] 
    private bool _isFavorite;
    
    public TimeSpan Duration { get; private set; }

    private const string UnknowArtist = "Неизвестный исполнитель";
    private const string Untitled = "Без названия";

    private AudioTrack(
        long id,
        Uri contentUri,
        AudioFormat format,
        string? artist,
        string? title,
        TimeSpan duration,
        bool isFavorite)
    {
        Id = id;
        ContentUri = contentUri;
        Format = format;
        Artist = artist;
        Title = title;
        Duration = duration;
        IsFavorite = isFavorite;
    }

    public void ToggleFavorite() => IsFavorite = !IsFavorite;

    public Result<Unit> RenameTitle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail(AudioTrackErrors.EmptyTitle);

        Title = name;
        return Result.Ok();
    }

    public Result<Unit> RenameArtist(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail(AudioTrackErrors.EmptyArtist);

        Artist = name;
        return Result.Ok();
    }


    public static Result<AudioTrack> Create(
        long id,
        Uri contentUri,
        AudioFormat format,
        string? artist,
        string? title,
        TimeSpan duration,
        bool isFavorite = false)

    {
        if (contentUri is null)
            return Result.Fail(AudioTrackErrors.EmptyContentUri);

        if (duration <= TimeSpan.Zero)
            return Result.Fail(AudioTrackErrors.NegativeOrZeroDuration);

        return Result.Ok(new AudioTrack(
            id,
            contentUri,
            format,
            artist,
            title,
            duration,
            isFavorite
        ));
    }
}
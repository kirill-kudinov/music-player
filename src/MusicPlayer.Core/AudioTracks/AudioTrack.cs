using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Core.AudioTracks;

public partial class AudioTrack : ObservableObject
{
    private const string UnknownArtist = "Неизвестный исполнитель";
    private const string Untitled = "Без названия";

    public AudioFormat Format { get; }
    public Uri ContentUri { get; }

    public string Title
    {
        get;
        private set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? Untitled : value.Trim();
            SetProperty(ref field, normalized);
        }
    } = Untitled;

    public string Artist
    {
        get;
        private set
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? UnknownArtist : value.Trim();
            SetProperty(ref field, normalized);
        }
    } = UnknownArtist;

    public bool IsFavorite
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public TimeSpan Duration { get; private set; }

    private AudioTrack(
        Uri contentUri,
        AudioFormat format,
        string? artist,
        string? title,
        TimeSpan duration,
        bool isFavorite)
    {
        ContentUri = contentUri;
        Format = format;
        Artist = artist ?? UnknownArtist;
        Title = title ?? Untitled;
        Duration = duration;
        IsFavorite = isFavorite;
    }

    public void ToggleFavorite() => IsFavorite = !IsFavorite;

    public void SetFavorite(bool isFavorite) => IsFavorite = isFavorite;

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
            contentUri,
            format,
            artist,
            title,
            duration,
            isFavorite));
    }

    public static Result<AudioTrack> Create(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return Result.Fail(AudioTrackErrors.InvalidFilePath);

        try
        {
            string fullPath = Path.GetFullPath(filePath);
            if (!Uri.TryCreate(fullPath, UriKind.Absolute, out Uri? uri))
                return Result.Fail(AudioTrackErrors.EmptyContentUri);

            using TagLib.File file = TagLib.File.Create(fullPath);

            string? artist = !string.IsNullOrWhiteSpace(file.Tag.FirstPerformer)
                ? file.Tag.FirstPerformer
                : file.Tag.FirstAlbumArtist;

            string title = !string.IsNullOrWhiteSpace(file.Tag.Title)
                ? file.Tag.Title
                : Path.GetFileNameWithoutExtension(fullPath);

            return Create(
                uri,
                GetFormat(fullPath),
                artist,
                title,
                file.Properties.Duration);
        }
        catch (Exception)
        {
            return Result.Fail(AudioTrackErrors.MetadataReadFailed);
        }
    }

    private static AudioFormat GetFormat(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".aac" => AudioFormat.Aac,
            ".flac" => AudioFormat.Flac,
            ".ogg" => AudioFormat.Ogg,
            ".opus" => AudioFormat.Opus,
            ".mp3" or ".mp4" => AudioFormat.Aac,
            _ => AudioFormat.Unknown
        };
    }
}
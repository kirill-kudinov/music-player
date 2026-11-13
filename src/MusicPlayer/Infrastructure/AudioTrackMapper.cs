using System;
using MusicPlayer.Core.AudioTracks;

namespace MusicPlayer.Infrastructure;

public static class AudioTrackMapper
{
    public static AudioTrackItem ToItem(AudioTrack audioTrack) => new()
    {
        Id = audioTrack.Id,
        Title = audioTrack.Title,
        Artist = audioTrack.Artist,
        Duration = audioTrack.Duration.ToString(),
        IsFavorite = audioTrack.IsFavorite,
        Uri = audioTrack.ContentUri.ToString(),
        AudioFormat = AudioFormatMapper.ToString(audioTrack.Format)
    };

    public static AudioTrack ToEntity(AudioTrackItem audioTrackItem)
    {
        var createResult = AudioTrack.Create(
            audioTrackItem.Id,
            new Uri(audioTrackItem.Uri),
            AudioFormatMapper.ToEntity(audioTrackItem.AudioFormat),
            audioTrackItem.Artist,
            audioTrackItem.Title,
            TimeSpan.Parse(audioTrackItem.Duration),
            audioTrackItem.IsFavorite
        );

        createResult.TryGetOk(out var audioTrack);

        return audioTrack!;
    }
}
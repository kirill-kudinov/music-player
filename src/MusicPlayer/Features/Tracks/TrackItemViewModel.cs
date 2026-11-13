using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;

namespace MusicPlayer.Features.Tracks;

public sealed class TrackItemViewModel(int id, AudioTrack track, ImageSource? cover)
{
    public int Id { get; } = id;
    public AudioTrack Track { get; } = track;
    public ImageSource? Cover { get; } = cover;

    public string Subtitle => $"{Track.Artist} • {Track.Duration:m\\:ss}";
}
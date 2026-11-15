using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;

namespace MusicPlayer.Features.Player;

public sealed partial class QueueTrackItemViewModel : ObservableObject
{
    public AudioTrack Track { get; }
    public ImageSource? Cover { get; }
    public string Subtitle { get; }

    [ObservableProperty]
    private bool _isCurrent;

    public QueueTrackItemViewModel(AudioTrack track, ImageSource? cover, bool isCurrent)
    {
        Track = track;
        Cover = cover;
        _isCurrent = isCurrent;

        int totalMinutes = (int)track.Duration.TotalMinutes;
        Subtitle = $"{track.Artist} • {totalMinutes}:{track.Duration.Seconds:D2}";
    }
}

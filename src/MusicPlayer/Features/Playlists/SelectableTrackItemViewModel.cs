using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;

namespace MusicPlayer.Features.Playlists;

public sealed partial class SelectableTrackItemViewModel : ObservableObject
{
    public AudioTrack Track { get; }

    public ImageSource? Cover { get; }

    public string Subtitle { get; }

    public bool IsAlreadyInPlaylist { get; }

    public bool CanToggleSelection => !IsAlreadyInPlaylist;

    public double ItemOpacity => IsAlreadyInPlaylist ? 0.55 : 1.0;

    [ObservableProperty]
    private bool _isSelected;

    public SelectableTrackItemViewModel(AudioTrack track, ImageSource? cover, bool isAlreadyInPlaylist)
    {
        Track = track;
        Cover = cover;
        IsAlreadyInPlaylist = isAlreadyInPlaylist;
        _isSelected = isAlreadyInPlaylist;

        int totalMinutes = (int)track.Duration.TotalMinutes;
        Subtitle = $"{track.Artist} • {totalMinutes}:{track.Duration.Seconds:D2}";
    }
}

using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.Playlists;

namespace MusicPlayer.Features.Tracks;

public sealed partial class SelectablePlaylistItemViewModel : ObservableObject
{
    private static readonly ImageSource FavoritesCoverImage = ImageSource.FromFile("playlist_favorites_cover.png");
    private static readonly ImageSource PlaceholderCoverImage = ImageSource.FromFile("playlist_cover_placeholder.png");

    public Playlist Playlist { get; }

    public Guid Id => Playlist.Id;

    public string Title => Playlist.Title;

    public bool IsSystem => Playlist.IsSystem;

    public bool InitiallyContainsTrack { get; }

    public string TrackCountText { get; }

    public ImageSource CoverSource { get; }

    [ObservableProperty]
    private bool _isSelected;

    public SelectablePlaylistItemViewModel(Playlist playlist, bool containsTrack)
    {
        Playlist = playlist;
        InitiallyContainsTrack = containsTrack;
        _isSelected = containsTrack;
        TrackCountText = FormatTrackCount(playlist.TrackCount);
        CoverSource = ResolveCoverSource(playlist);
    }

    private static ImageSource ResolveCoverSource(Playlist playlist)
    {
        if (playlist.IsSystem)
            return FavoritesCoverImage;

        if (playlist.CoverPath.TryGetSome(out string? coverPath) && File.Exists(coverPath))
            return ImageSource.FromFile(coverPath);

        return PlaceholderCoverImage;
    }

    private static string FormatTrackCount(int count)
    {
        int abs = Math.Abs(count);
        int mod100 = abs % 100;
        if (mod100 is >= 11 and <= 19)
            return $"{count} треков";

        return (abs % 10) switch
        {
            1 => $"{count} трек",
            2 or 3 or 4 => $"{count} трека",
            _ => $"{count} треков"
        };
    }
}
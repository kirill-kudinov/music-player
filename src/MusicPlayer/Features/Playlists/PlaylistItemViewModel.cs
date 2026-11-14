using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.Playlists;

namespace MusicPlayer.Features.Playlists;

public sealed partial class PlaylistItemViewModel : ObservableObject
{
    private static readonly ImageSource FavoritesCoverImage = ImageSource.FromFile("playlist_favorites_cover.png");
    private static readonly ImageSource PlaceholderCoverImage = ImageSource.FromFile("playlist_cover_placeholder.png");

    private ImageSource _coverSource;

    public Playlist Playlist { get; }

    public Guid Id => Playlist.Id;

    public string Title => Playlist.Title;

    public bool IsSystem => Playlist.IsSystem;

    public bool CanEdit => !Playlist.IsSystem;

    public int TrackCount => Playlist.TrackCount;

    public string TrackCountText => FormatTrackCount(Playlist.TrackCount);

    public ImageSource CoverSource => _coverSource;

    public PlaylistItemViewModel(Playlist playlist)
    {
        Playlist = playlist;
        _coverSource = ResolveCoverSource();
    }

    public void Refresh()
    {
        _coverSource = ResolveCoverSource();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(TrackCount));
        OnPropertyChanged(nameof(TrackCountText));
        OnPropertyChanged(nameof(CoverSource));
    }

    private ImageSource ResolveCoverSource()
    {
        if (Playlist.IsSystem)
            return FavoritesCoverImage;

        if (Playlist.CoverPath.TryGetSome(out string? coverPath) && File.Exists(coverPath))
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

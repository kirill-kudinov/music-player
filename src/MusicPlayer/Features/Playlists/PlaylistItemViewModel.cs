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

    public string TrackCountText => PlaylistFormatting.FormatTrackCount(Playlist.TrackCount);

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
}

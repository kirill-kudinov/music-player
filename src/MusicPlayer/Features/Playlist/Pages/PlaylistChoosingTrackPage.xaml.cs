using Microsoft.Maui.Controls;
using MusicPlayer.Features.Playlist.ViewModels;

namespace MusicPlayer.Features.Playlist.Pages;

[QueryProperty(nameof(PlaylistId), "PlaylistId")]
public partial class PlaylistChoosingTrackPage
{
    public int PlaylistId
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    private readonly PlaylistChoosingTrackViewModel _playlistChoosingTrackViewModel;

    public PlaylistChoosingTrackPage(
        PlaylistChoosingTrackViewModel playlistChoosingTrackViewModel
    )
    {
        _playlistChoosingTrackViewModel = playlistChoosingTrackViewModel;

        InitializeComponent();

        BindingContext = playlistChoosingTrackViewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _playlistChoosingTrackViewModel.PlaylistId = PlaylistId;
        await _playlistChoosingTrackViewModel.LoadTracksAsync();
    }
}
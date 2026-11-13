using Microsoft.Maui.Controls;
using MusicPlayer.Features.Player.Animations;
using MusicPlayer.Features.Playlist.ViewModels;

namespace MusicPlayer.Features.Playlist.Pages;

[QueryProperty(nameof(Playlist), "Playlist")]
public partial class PlaylistPage
{
    public required Core.Playlist.Playlist Playlist
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    private readonly PlaylistViewModel _playlistViewModel;
    private readonly IPlayerTransitionService _transitionService;

    public PlaylistPage(
        PlaylistViewModel playlistViewModel,
        IPlayerTransitionService transitionService
    )
    {
        _playlistViewModel = playlistViewModel;
        _transitionService = transitionService;

        InitializeComponent();

        BindingContext = playlistViewModel;

        transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _transitionService.RegisterMiniPlayerCallbacks(
            MiniPlayer.PlayExpandOutAnimationAsync,
            MiniPlayer.PlayCollapseInAnimationAsync);

        _playlistViewModel.Playlist = Playlist;
        await _playlistViewModel.LoadTracksAsync();
    }
}
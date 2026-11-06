using Microsoft.Maui.Controls;

namespace MusicPlayer.Features.Tracks;

public partial class TracksPage : ContentPage
{
    private TracksViewModel _tracksViewModel;
    
    public TracksPage(TracksViewModel tracksViewModel)
    {
        _tracksViewModel = tracksViewModel;
        
        InitializeComponent();

        BindingContext = tracksViewModel;
        
        tracksViewModel.LoadTracks();
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _tracksViewModel.LoadTracks();
    }
}
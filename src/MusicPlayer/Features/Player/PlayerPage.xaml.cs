using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace MusicPlayer.Features.Player;

public partial class PlayerPage : ContentPage
{
    private readonly PlayerViewModel _viewModel;
    private readonly IDispatcherTimer _pollingTimer;

    public PlayerPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _pollingTimer = Dispatcher.CreateTimer();
        _pollingTimer.Interval = TimeSpan.FromMilliseconds(250);
        _pollingTimer.Tick += (_, _) => _viewModel.Tick();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Tick();
        _pollingTimer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _pollingTimer.Stop();
    }
}
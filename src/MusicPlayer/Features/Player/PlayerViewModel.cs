using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Player;

public sealed partial class PlayerViewModel : ObservableObject
{
    private readonly IAudioPlayer _player;
    private readonly IPlaybackQueue _queue;
    private readonly ITrackCoverExtractor _coverExtractor;

    private bool _isUserSeeking;

    [ObservableProperty]
    private string _playContextTitle = "Плейлист «Избранное»";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentTrackFavorite))]
    private AudioTrack? _currentTrack;

    [ObservableProperty]
    private ImageSource? _trackCover;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private PlaybackRepeatMode _repeatMode = PlaybackRepeatMode.Off;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionSeconds))]
    private TimeSpan _position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationSeconds))]
    private TimeSpan _duration;

    public bool IsCurrentTrackFavorite => CurrentTrack?.IsFavorite ?? false;
    public double PositionSeconds => Position.TotalSeconds;
    public double DurationSeconds => Duration.TotalSeconds > 0 ? Duration.TotalSeconds : 1.0;

    public bool CanSkipNext => _queue.HasNext;
    public bool CanSkipPrevious => _queue.HasPrevious;

    public PlayerViewModel(
        IAudioPlayer player,
        IPlaybackQueue queue,
        ITrackCoverExtractor coverExtractor)
    {
        _player = player;
        _queue = queue;
        _coverExtractor = coverExtractor;

        SyncState();
    }

    partial void OnCurrentTrackChanged(AudioTrack? value)
    {
        TrackCover = _coverExtractor.ExtractCover(value?.ContentUri);
    }

    public void Tick()
    {
        if (IsPlaying != _player.IsPlaying)
            IsPlaying = _player.IsPlaying;

        if (CurrentTrack != _queue.CurrentTrack)
            SyncState();

        if (!_isUserSeeking)
            Position = _player.Position;
    }

    [RelayCommand]
    private static async Task CollapseAsync() => await Shell.Current.GoToAsync("..");

    [RelayCommand]
    private static Task OpenQueueAsync() => Task.CompletedTask;

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_player.IsPlaying)
        {
            _player.Pause();
        }
        else
        {
            if (_player.Position == TimeSpan.Zero && _queue.CurrentTrack is not null)
                _player.Play(_queue.CurrentTrack.ContentUri);
            else
                _player.Resume();
        }

        IsPlaying = _player.IsPlaying;
    }

    [RelayCommand(CanExecute = nameof(CanSkipNext))]
    private void NextTrack()
    {
        var next = _queue.MoveNext();
        if (next is not null)
            _player.Play(next.ContentUri);

        SyncState();
    }

    [RelayCommand(CanExecute = nameof(CanSkipPrevious))]
    private void PreviousTrack()
    {
        var prev = _queue.MovePrevious();
        if (prev is not null)
            _player.Play(prev.ContentUri);

        SyncState();
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (CurrentTrack is null)
            return;

        CurrentTrack.ToggleFavorite();
        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(IsCurrentTrackFavorite));
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        _queue.ToggleShuffle();
        IsShuffleEnabled = _queue.IsShuffleEnabled;
    }

    [RelayCommand]
    private void CycleRepeatMode()
    {
        _queue.CycleRepeatMode();
        RepeatMode = _queue.RepeatMode;
    }

    [RelayCommand]
    private void SeekStarted() => _isUserSeeking = true;

    [RelayCommand]
    private void SeekCompleted(double targetSeconds)
    {
        var targetPosition = TimeSpan.FromSeconds(targetSeconds);
        _player.Seek(targetPosition);

        Position = targetPosition;
        _isUserSeeking = false;
    }

    private void SyncState()
    {
        CurrentTrack = _queue.CurrentTrack;
        IsPlaying = _player.IsPlaying;
        IsShuffleEnabled = _queue.IsShuffleEnabled;
        RepeatMode = _queue.RepeatMode;
        Duration = CurrentTrack?.Duration ?? TimeSpan.Zero;

        NextTrackCommand.NotifyCanExecuteChanged();
        PreviousTrackCommand.NotifyCanExecuteChanged();
    }
}
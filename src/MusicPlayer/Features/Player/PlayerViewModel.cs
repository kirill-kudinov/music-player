using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Player.Animations;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Player;

public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly IAudioPlayer _player;
    private readonly IPlaybackQueue _queue;
    private readonly ITrackCoverExtractor _coverExtractor;
    private readonly IPlayerTransitionService _transitionService;

    private bool _isUserSeeking;
    private bool _isDisposed;

    [ObservableProperty]
    private string _playContextTitle = "Плейлист «Избранное»";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentTrackFavorite))]
    [NotifyPropertyChangedFor(nameof(HasCurrentTrack))]
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

    public bool HasCurrentTrack => CurrentTrack is not null;
    public bool IsCurrentTrackFavorite => CurrentTrack?.IsFavorite ?? false;

    public double DurationSeconds => Duration.TotalSeconds > 0d ? Duration.TotalSeconds : 1d;

    public double PositionSeconds
    {
        get => Position.TotalSeconds;
        set
        {
            if (!_isUserSeeking || double.IsNaN(value) || double.IsInfinity(value))
                return;

            double clamped = Math.Clamp(value, 0d, DurationSeconds);
            if (Math.Abs(Position.TotalSeconds - clamped) < 0.01d)
                return;

            Position = TimeSpan.FromSeconds(clamped);
        }
    }

    public bool CanSkipNext => _queue.HasNext;
    public bool CanSkipPrevious => _queue.HasPrevious;

    public PlayerViewModel(
        IAudioPlayer player,
        IPlaybackQueue queue,
        ITrackCoverExtractor coverExtractor,
        IPlayerTransitionService transitionService)
    {
        _player = player;
        _queue = queue;
        _coverExtractor = coverExtractor;
        _transitionService = transitionService;

        _player.PlaybackCompleted += OnPlaybackCompleted;

        SyncState();
    }

    partial void OnCurrentTrackChanged(AudioTrack? value)
    {
        TrackCover = _coverExtractor.ExtractCover(value?.ContentUri);
    }

    partial void OnDurationChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(PositionSeconds));
    }

    public void NotifyTrackStarted()
    {
        _isUserSeeking = false;
        Position = TimeSpan.Zero;
        SyncState();
        IsPlaying = true;
    }

    public void SyncQueueState()
    {
        SyncState();
    }

    public void SyncFavoriteState()
    {
        OnPropertyChanged(nameof(CurrentTrack));
        OnPropertyChanged(nameof(IsCurrentTrackFavorite));
    }

    public void HandleTrackRemoved(AudioTrack removedTrack)
    {
        bool wasCurrentTrack = ReferenceEquals(CurrentTrack, removedTrack) ||
                               (CurrentTrack is not null && CurrentTrack.ContentUri == removedTrack.ContentUri);
        bool wasPlaying = _player.IsPlaying;

        _queue.Remove(removedTrack);

        if (!wasCurrentTrack)
        {
            NotifySkipCommandsCanExecuteChanged();
            return;
        }

        _isUserSeeking = false;
        var nextCurrent = _queue.CurrentTrack;

        if (nextCurrent is null)
        {
            _player.Stop();
            CurrentTrack = null;
            IsPlaying = false;
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            NotifySkipCommandsCanExecuteChanged();
            return;
        }

        Position = TimeSpan.Zero;
        if (wasPlaying)
        {
            _player.Play(nextCurrent.ContentUri);
            SyncState();
            IsPlaying = true;
        }
        else
        {
            _player.Stop();
            SyncState();
            IsPlaying = false;
        }
    }

    public void Tick()
    {
        if (_transitionService.IsTransitioning)
            return;

        if (CurrentTrack != _queue.CurrentTrack)
            SyncState();

        bool playerIsPlaying = _player.IsPlaying;
        if (IsPlaying != playerIsPlaying)
            IsPlaying = playerIsPlaying;

        if (Duration <= TimeSpan.Zero && _player.Duration > TimeSpan.Zero)
            Duration = _player.Duration;

        if (!_isUserSeeking)
        {
            TimeSpan currentPosition = _player.Position;
            if (Math.Abs((currentPosition - Position).TotalMilliseconds) >= 40d)
            {
                Position = currentPosition;
            }
        }
    }

    [RelayCommand]
    private Task CollapseAsync() => _transitionService.CollapseAsync();

    [RelayCommand]
    private Task ExpandAsync() => _transitionService.ExpandAsync();

    [RelayCommand]
    private static Task OpenQueueAsync() => Task.CompletedTask;

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_player.IsPlaying)
        {
            _player.Pause();
            IsPlaying = false;
        }
        else
        {
            if (_player.Position == TimeSpan.Zero && _queue.CurrentTrack is not null)
                _player.Play(_queue.CurrentTrack.ContentUri);
            else
                _player.Resume();

            IsPlaying = true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSkipNext))]
    private void NextTrack()
    {
        var next = _queue.MoveNext();
        if (next is not null)
        {
            _isUserSeeking = false;
            Position = TimeSpan.Zero;
            _player.Play(next.ContentUri);
            SyncState();
            IsPlaying = true;
            return;
        }

        SyncState();
    }

    [RelayCommand(CanExecute = nameof(CanSkipPrevious))]
    private void PreviousTrack()
    {
        var prev = _queue.MovePrevious();
        if (prev is not null)
        {
            _isUserSeeking = false;
            Position = TimeSpan.Zero;
            _player.Play(prev.ContentUri);
            SyncState();
            IsPlaying = true;
            return;
        }

        SyncState();
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (CurrentTrack is null)
            return;

        CurrentTrack.ToggleFavorite();
        SyncFavoriteState();
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        _queue.ToggleShuffle();
        IsShuffleEnabled = _queue.IsShuffleEnabled;
        NotifySkipCommandsCanExecuteChanged();
    }

    [RelayCommand]
    private void CycleRepeatMode()
    {
        _queue.CycleRepeatMode();
        RepeatMode = _queue.RepeatMode;
        NotifySkipCommandsCanExecuteChanged();
    }

    [RelayCommand]
    private void SeekStarted()
    {
        _isUserSeeking = true;
    }

    [RelayCommand]
    private void SeekCompleted()
    {
        if (!_isUserSeeking)
            return;

        TimeSpan targetPosition = Position;
        _player.Seek(targetPosition);
        _isUserSeeking = false;
        OnPropertyChanged(nameof(PositionSeconds));
    }

    private void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_queue.HasNext)
            {
                NextTrack();
            }
            else
            {
                _isUserSeeking = false;
                IsPlaying = false;
                Position = TimeSpan.Zero;
            }
        });
    }

    private void SyncState()
    {
        CurrentTrack = _queue.CurrentTrack;
        IsPlaying = _player.IsPlaying;
        IsShuffleEnabled = _queue.IsShuffleEnabled;
        RepeatMode = _queue.RepeatMode;

        TimeSpan trackDuration = CurrentTrack?.Duration ?? TimeSpan.Zero;
        Duration = trackDuration > TimeSpan.Zero ? trackDuration : _player.Duration;

        if (!_isUserSeeking)
        {
            Position = _player.Position;
        }

        NotifySkipCommandsCanExecuteChanged();
    }

    private void NotifySkipCommandsCanExecuteChanged()
    {
        NextTrackCommand.NotifyCanExecuteChanged();
        PreviousTrackCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _player.PlaybackCompleted -= OnPlaybackCompleted;
    }
}
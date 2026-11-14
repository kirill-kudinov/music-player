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
    private static readonly TimeSpan PreviousTrackRestartThreshold = TimeSpan.FromSeconds(4);
    private const long SeekStabilizationWindowMs = 350;

    private readonly IAudioPlayer _player;
    private readonly IPlaybackQueue _queue;
    private readonly ITrackCoverExtractor _coverExtractor;
    private readonly IPlayerTransitionService _transitionService;

    private bool _isUserSeeking;
    private bool _isSyncingFromEngine;
    private long _lastSeekCommitTimestampMs;
    private int _lastDisplayedPositionSecond = -1;
    private int _lastDisplayedDurationSecond = -1;
    private bool _isDisposed;

    [ObservableProperty]
    private string _playContextTitle = "Сейчас играет";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCurrentTrackFavorite))]
    [NotifyPropertyChangedFor(nameof(HasCurrentTrack))]
    [NotifyPropertyChangedFor(nameof(CanSkipPrevious))]
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
    private TimeSpan _position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationSeconds))]
    private TimeSpan _duration;

    [ObservableProperty]
    private string _formattedPosition = "0:00";

    [ObservableProperty]
    private string _formattedDuration = "0:00";

    public bool HasCurrentTrack => CurrentTrack is not null;

    public bool IsCurrentTrackFavorite => CurrentTrack?.IsFavorite ?? false;

    public double DurationSeconds => Duration.TotalSeconds > 0.1d ? Duration.TotalSeconds : 1d;

    /// <summary>
    /// Привязано к Slider.Value (TwoWay).
    /// Поддерживает как плавное перетаскивание ползунка (Drag), так и одиночный тап по дорожке Slider.
    /// </summary>
    public double PositionSeconds
    {
        get => Math.Clamp(Position.TotalSeconds, 0d, DurationSeconds);
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || _isSyncingFromEngine)
                return;

            double clamped = Math.Clamp(value, 0d, DurationSeconds);
            if (Math.Abs(Position.TotalSeconds - clamped) < 0.005d)
                return;

            var targetTime = TimeSpan.FromSeconds(clamped);
            Position = targetTime;

            // Если пользователь тапнул по точке на слайдере без удержания ползунка — сразу применяем перемотку
            if (!_isUserSeeking)
            {
                CommitSeek(targetTime);
            }
        }
    }

    public bool CanSkipNext => _queue.HasNext;

    /// <summary>
    /// Кнопка Previous активна всегда, когда есть текущий трек (так как после 4 сек она отматывает в начало)
    /// либо когда в очереди есть предыдущий трек.
    /// </summary>
    public bool CanSkipPrevious => HasCurrentTrack || _queue.HasPrevious;

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
        PreviousTrackCommand.NotifyCanExecuteChanged();
    }

    partial void OnPositionChanged(TimeSpan value)
    {
        int currentSecond = (int)Math.Floor(Math.Max(0d, value.TotalSeconds));
        if (currentSecond != _lastDisplayedPositionSecond)
        {
            _lastDisplayedPositionSecond = currentSecond;
            FormattedPosition = FormatTimeSpan(TimeSpan.FromSeconds(currentSecond));
        }

        // Не отправляем обратный PropertyChanged в Slider, пока палец пользователя двигает ползунок,
        // чтобы избежать микрофризов и борьбы привязок в UI-потоке.
        if (!_isUserSeeking)
        {
            OnPropertyChanged(nameof(PositionSeconds));
        }
    }

    partial void OnDurationChanged(TimeSpan value)
    {
        int durationSecond = (int)Math.Floor(Math.Max(0d, value.TotalSeconds));
        if (durationSecond != _lastDisplayedDurationSecond)
        {
            _lastDisplayedDurationSecond = durationSecond;
            FormattedDuration = FormatTimeSpan(TimeSpan.FromSeconds(durationSecond));
        }

        if (!_isUserSeeking)
        {
            OnPropertyChanged(nameof(PositionSeconds));
        }
    }

    public void NotifyTrackStarted()
    {
        _isUserSeeking = false;
        _lastSeekCommitTimestampMs = Environment.TickCount64;
        SetEnginePosition(TimeSpan.Zero);
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
            SetEnginePosition(TimeSpan.Zero);
            Duration = TimeSpan.Zero;
            NotifySkipCommandsCanExecuteChanged();
            return;
        }

        SetEnginePosition(TimeSpan.Zero);

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
        {
            SyncState();
        }

        bool playerIsPlaying = _player.IsPlaying;
        if (IsPlaying != playerIsPlaying)
        {
            IsPlaying = playerIsPlaying;
        }

        if (Duration <= TimeSpan.Zero && _player.Duration > TimeSpan.Zero)
        {
            Duration = _player.Duration;
        }

        if (_isUserSeeking)
            return;

        // Во время окна стабилизации после перемотки не позволяем старому значению из таймера отбросить ползунок назад
        if (Environment.TickCount64 - _lastSeekCommitTimestampMs < SeekStabilizationWindowMs)
            return;

        TimeSpan currentPosition = _player.Position;
        if (Math.Abs((currentPosition - Position).TotalMilliseconds) >= 35d)
        {
            SetEnginePosition(currentPosition);
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
            {
                _player.Play(_queue.CurrentTrack.ContentUri);
            }
            else
            {
                _player.Resume();
            }

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
            _lastSeekCommitTimestampMs = Environment.TickCount64;
            SetEnginePosition(TimeSpan.Zero);
            _player.Play(next.ContentUri);
            SyncState();
            IsPlaying = true;
            return;
        }

        SyncState();
    }

    /// <summary>
    /// Если текущий трек играет больше 4 секунд — отматывает текущий трек в начало (0:00).
    /// Если играет 4 секунды или меньше — переключает на предыдущий трек в очереди.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSkipPrevious))]
    private void PreviousTrack()
    {
        TimeSpan effectivePosition = GetEffectivePosition();

        // 1. Если трек проигрывается более 4 секунд (или предыдущего трека нет в очереди) -> отматываем в начало
        if (effectivePosition > PreviousTrackRestartThreshold || !_queue.HasPrevious)
        {
            RestartCurrentTrack();
            return;
        }

        // 2. Если трек играет <= 4 секунд и есть предыдущий трек -> переключаем на предыдущий трек
        var prev = _queue.MovePrevious();
        if (prev is not null)
        {
            _isUserSeeking = false;
            _lastSeekCommitTimestampMs = Environment.TickCount64;
            SetEnginePosition(TimeSpan.Zero);
            _player.Play(prev.ContentUri);
            SyncState();
            IsPlaying = true;
            return;
        }

        RestartCurrentTrack();
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

        _isUserSeeking = false;
        CommitSeek(Position);
    }

    private void RestartCurrentTrack()
    {
        if (CurrentTrack is null)
            return;

        _isUserSeeking = false;
        CommitSeek(TimeSpan.Zero);

        if (!_player.IsPlaying)
        {
            _player.Resume();
            IsPlaying = true;
        }
    }

    private void CommitSeek(TimeSpan targetPosition)
    {
        TimeSpan clamped = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0d, DurationSeconds));
        _lastSeekCommitTimestampMs = Environment.TickCount64;
        SetEnginePosition(clamped);
        _player.Seek(clamped);
    }

    private TimeSpan GetEffectivePosition()
    {
        if (_isUserSeeking)
            return Position;

        if (Environment.TickCount64 - _lastSeekCommitTimestampMs < SeekStabilizationWindowMs)
            return Position;

        TimeSpan enginePos = _player.Position;
        return enginePos > TimeSpan.Zero ? enginePos : Position;
    }

    private void SetEnginePosition(TimeSpan newPosition)
    {
        _isSyncingFromEngine = true;
        try
        {
            Position = newPosition;
        }
        finally
        {
            _isSyncingFromEngine = false;
        }
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
                SetEnginePosition(TimeSpan.Zero);
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

        if (!_isUserSeeking && Environment.TickCount64 - _lastSeekCommitTimestampMs >= SeekStabilizationWindowMs)
        {
            SetEnginePosition(_player.Position);
        }

        NotifySkipCommandsCanExecuteChanged();
    }

    private void NotifySkipCommandsCanExecuteChanged()
    {
        NextTrackCommand.NotifyCanExecuteChanged();
        PreviousTrackCommand.NotifyCanExecuteChanged();
    }

    private static string FormatTimeSpan(TimeSpan time)
    {
        int totalMinutes = (int)time.TotalMinutes;
        return $"{totalMinutes}:{time.Seconds:D2}";
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _player.PlaybackCompleted -= OnPlaybackCompleted;
    }
}
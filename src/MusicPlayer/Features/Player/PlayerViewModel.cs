using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Player.Animations;
using MusicPlayer.Features.Playlists.Data;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Player;

public sealed partial class PlayerViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan PreviousTrackRestartThreshold = TimeSpan.FromSeconds(4);
    private const long SeekStabilizationWindowMs = 350;

    private readonly IAudioPlayer _player;
    private readonly IPlaybackQueue _queue;
    private readonly IPlayerTransitionService _transitionService;
    private readonly IPlaylistRepository _playlistRepository;
    private readonly PlayerAppearanceController _appearance;
    private readonly MediaNotificationBridge _notificationBridge;

    private bool _isUserSeeking;
    private bool _isSyncingFromEngine;
    private long _lastSeekCommitTimestampMs;
    private int _lastDisplayedPositionSecond = -1;
    private int _lastDisplayedDurationSecond = -1;
    private bool _isDisposed;

    public PlayerQueueViewModel Queue { get; }

    [ObservableProperty]
    private string _playContextTitle = "Сейчас играет";

    private AudioTrack? _currentTrack;

    public AudioTrack? CurrentTrack
    {
        get => _currentTrack;
        set
        {
            if (SetProperty(ref _currentTrack, value))
            {
                OnPropertyChanged(nameof(IsCurrentTrackFavorite));
                OnPropertyChanged(nameof(HasCurrentTrack));
                OnPropertyChanged(nameof(CanSkipPrevious));
                HandleCurrentTrackChanged(value);
            }
        }
    }

    [ObservableProperty]
    private ImageSource? _trackCover;

    [ObservableProperty]
    private Color _dominantGradientColor = PlayerAppearanceController.DefaultWineColor;

    private bool _isPlaying;
    private TimeSpan _position;
    private TimeSpan _duration;

    [ObservableProperty]
    private bool _isShuffleEnabled;

    [ObservableProperty]
    private PlaybackRepeatMode _repeatMode = PlaybackRepeatMode.Off;

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                _notificationBridge.Sync(CurrentTrack, value, Position);
            }
        }
    }

    public TimeSpan Position
    {
        get => _position;
        set
        {
            if (SetProperty(ref _position, value))
            {
                HandlePositionChanged(value);
            }
        }
    }

    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (SetProperty(ref _duration, value))
            {
                OnPropertyChanged(nameof(DurationSeconds));
                HandleDurationChanged(value);
            }
        }
    }

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

            if (!_isUserSeeking)
            {
                CommitSeek(targetTime);
            }
        }
    }

    public bool CanSkipNext => _queue.HasNext;

    public bool CanSkipPrevious => HasCurrentTrack || _queue.HasPrevious;

    public PlayerViewModel(
        IAudioPlayer player,
        IPlaybackQueue queue,
        ITrackCoverExtractor coverExtractor,
        IPlayerTransitionService transitionService,
        IPlaylistRepository playlistRepository,
        IMediaNotificationService? mediaNotificationService = null)
    {
        _player = player;
        _queue = queue;
        _transitionService = transitionService;
        _playlistRepository = playlistRepository;

        Queue = new PlayerQueueViewModel(queue, coverExtractor);
        Queue.AttachCallbacks(PlayFromQueueTrack, HandleTrackRemoved, NotifySkipCommandsCanExecuteChanged);

        _appearance = new PlayerAppearanceController(coverExtractor);
        _notificationBridge = new MediaNotificationBridge(
            mediaNotificationService,
            onPlay: () => { if (CurrentTrack is not null && !IsPlaying) TogglePlayPause(); },
            onPause: () => { if (IsPlaying) TogglePlayPause(); },
            onNext: () => { if (NextTrackCommand.CanExecute(null)) NextTrack(); },
            onPrevious: () => { if (PreviousTrackCommand.CanExecute(null)) PreviousTrack(); },
            onSeek: pos => { if (CurrentTrack is not null) CommitSeek(pos); },
            onStop: DismissMiniPlayer);

        _player.PlaybackCompleted += OnPlaybackCompleted;
        _playlistRepository.TrackFavoriteChanged += OnRepositoryTrackFavoriteChanged;
        SyncState();
    }

    private void HandleCurrentTrackChanged(AudioTrack? value)
    {
        PreviousTrackCommand.NotifyCanExecuteChanged();
        (ImageSource? cover, Color fallback) = _appearance.UpdateForTrack(
            value?.ContentUri,
            color => DominantGradientColor = color);

        TrackCover = cover;
        if (value is null)
        {
            DominantGradientColor = fallback;
        }
    }

    private void HandlePositionChanged(TimeSpan value)
    {
        int currentSecond = (int)Math.Floor(Math.Max(0d, value.TotalSeconds));
        if (currentSecond != _lastDisplayedPositionSecond)
        {
            _lastDisplayedPositionSecond = currentSecond;
            FormattedPosition = FormatTimeSpan(TimeSpan.FromSeconds(currentSecond));
        }

        if (!_isUserSeeking)
        {
            OnPropertyChanged(nameof(PositionSeconds));
        }
    }

    private void HandleDurationChanged(TimeSpan value)
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
        _notificationBridge.Sync(CurrentTrack, IsPlaying, Position);
    }

    public void SyncQueueState() => SyncState();

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
            _notificationBridge.Clear();
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
    private void DismissMiniPlayer()
    {
        _isUserSeeking = false;
        _player.Stop();
        _queue.Clear();
        CurrentTrack = null;
        IsPlaying = false;
        SetEnginePosition(TimeSpan.Zero);
        Duration = TimeSpan.Zero;
        NotifySkipCommandsCanExecuteChanged();
        Queue.IsOpen = false;
        _notificationBridge.Clear();
    }

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
            PlayTrackImmediate(next);
            return;
        }

        SyncState();
    }

    [RelayCommand(CanExecute = nameof(CanSkipPrevious))]
    private void PreviousTrack()
    {
        TimeSpan effectivePosition = GetEffectivePosition();

        if (effectivePosition > PreviousTrackRestartThreshold || !_queue.HasPrevious)
        {
            RestartCurrentTrack();
            return;
        }

        var prev = _queue.MovePrevious();
        if (prev is not null)
        {
            PlayTrackImmediate(prev);
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
        _ = _playlistRepository.SetTrackFavoriteAsync(CurrentTrack, CurrentTrack.IsFavorite);
    }

    [RelayCommand]
    private void ToggleShuffle()
    {
        _queue.ToggleShuffle();
        IsShuffleEnabled = _queue.IsShuffleEnabled;
        NotifySkipCommandsCanExecuteChanged();
        if (Queue.IsOpen)
        {
            Queue.ReorderToMatchQueue();
        }
    }

    [RelayCommand]
    private void CycleRepeatMode()
    {
        _queue.CycleRepeatMode();
        RepeatMode = _queue.RepeatMode;
        NotifySkipCommandsCanExecuteChanged();
    }

    [RelayCommand]
    private void SeekStarted() => _isUserSeeking = true;

    [RelayCommand]
    private void SeekCompleted()
    {
        if (!_isUserSeeking)
            return;

        _isUserSeeking = false;
        CommitSeek(Position);
    }

    private void PlayFromQueueTrack(AudioTrack track) => PlayTrackImmediate(track);

    private void PlayTrackImmediate(AudioTrack track)
    {
        _isUserSeeking = false;
        _lastSeekCommitTimestampMs = Environment.TickCount64;
        SetEnginePosition(TimeSpan.Zero);
        _player.Play(track.ContentUri);
        SyncState();
        IsPlaying = true;
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
        _notificationBridge.Sync(CurrentTrack, IsPlaying, clamped);
    }

    private TimeSpan GetEffectivePosition()
    {
        if (_isUserSeeking || Environment.TickCount64 - _lastSeekCommitTimestampMs < SeekStabilizationWindowMs)
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

    private void OnRepositoryTrackFavoriteChanged(object? sender, TrackFavoriteChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (CurrentTrack is not null &&
                string.Equals(CurrentTrack.ContentUri.OriginalString, e.ContentUri.OriginalString, StringComparison.OrdinalIgnoreCase))
            {
                CurrentTrack.SetFavorite(e.IsFavorite);
                SyncFavoriteState();
            }
        });
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
        if (Queue.IsOpen)
        {
            Queue.UpdateCurrentMarkers();
        }
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
        _appearance.Dispose();
        _notificationBridge.Dispose();
        _player.PlaybackCompleted -= OnPlaybackCompleted;
        _playlistRepository.TrackFavoriteChanged -= OnRepositoryTrackFavoriteChanged;
    }
}
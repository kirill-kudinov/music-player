using System;
using System.Collections.Generic;
using System.Linq;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Player;

namespace MusicPlayer.Playback;

public partial class PlaybackQueue : IPlaybackQueue
{
    private readonly List<AudioTrack> _originalTracks = [];
    private readonly List<AudioTrack> _playbackTracks = [];
    private readonly Random _random = new();

    private int _currentIndex = -1;

    public AudioTrack? CurrentTrack =>
        _currentIndex >= 0 && _currentIndex < _playbackTracks.Count
            ? _playbackTracks[_currentIndex]
            : null;

    public bool HasNext => _playbackTracks.Count > 0 &&
        (RepeatMode != PlaybackRepeatMode.Off || _currentIndex < _playbackTracks.Count - 1);

    public bool HasPrevious => _playbackTracks.Count > 0 &&
        (RepeatMode != PlaybackRepeatMode.Off || _currentIndex > 0);

    public bool IsShuffleEnabled { get; private set; }
    public PlaybackRepeatMode RepeatMode { get; private set; } = PlaybackRepeatMode.Off;

    public void SetTracks(IReadOnlyCollection<AudioTrack> tracks, AudioTrack current)
    {
        _originalTracks.Clear();
        _originalTracks.AddRange(tracks);

        RebuildPlaybackList(current);
    }

    public void Enqueue(AudioTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (!_originalTracks.Contains(track))
        {
            _originalTracks.Add(track);
        }

        if (_playbackTracks.Count == 0 || _currentIndex < 0)
        {
            _playbackTracks.Add(track);
            _currentIndex = 0;
            return;
        }

        int insertIndex = Math.Clamp(_currentIndex + 1, 0, _playbackTracks.Count);
        _playbackTracks.Insert(insertIndex, track);
    }

    public bool Remove(AudioTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        bool removedFromOriginal = _originalTracks.Remove(track);
        bool removedFromPlayback = false;

        for (int i = _playbackTracks.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_playbackTracks[i], track) && _playbackTracks[i].ContentUri != track.ContentUri)
                continue;

            _playbackTracks.RemoveAt(i);
            removedFromPlayback = true;

            if (i < _currentIndex)
            {
                _currentIndex--;
            }
            else if (i == _currentIndex && _currentIndex >= _playbackTracks.Count)
            {
                _currentIndex = _playbackTracks.Count - 1;
            }
        }

        if (_playbackTracks.Count == 0)
        {
            _currentIndex = -1;
        }

        return removedFromOriginal || removedFromPlayback;
    }

    public AudioTrack? MoveNext()
    {
        if (_playbackTracks.Count == 0)
            return null;

        if (RepeatMode == PlaybackRepeatMode.One)
            return CurrentTrack;

        if (_currentIndex < _playbackTracks.Count - 1)
        {
            _currentIndex++;
            return _playbackTracks[_currentIndex];
        }

        if (RepeatMode == PlaybackRepeatMode.All)
        {
            _currentIndex = 0;
            return _playbackTracks[_currentIndex];
        }

        return null;
    }

    public AudioTrack? MovePrevious()
    {
        if (_playbackTracks.Count == 0)
            return null;

        if (RepeatMode == PlaybackRepeatMode.One)
            return CurrentTrack;

        if (_currentIndex > 0)
        {
            _currentIndex--;
            return _playbackTracks[_currentIndex];
        }

        if (RepeatMode == PlaybackRepeatMode.All)
        {
            _currentIndex = _playbackTracks.Count - 1;
            return _playbackTracks[_currentIndex];
        }

        return null;
    }

    public void ToggleShuffle()
    {
        IsShuffleEnabled = !IsShuffleEnabled;
        var current = CurrentTrack;
        RebuildPlaybackList(current);
    }

    public void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            PlaybackRepeatMode.Off => PlaybackRepeatMode.All,
            PlaybackRepeatMode.All => PlaybackRepeatMode.One,
            PlaybackRepeatMode.One => PlaybackRepeatMode.Off,
            _ => PlaybackRepeatMode.Off
        };
    }

    private void RebuildPlaybackList(AudioTrack? keepCurrent)
    {
        _playbackTracks.Clear();

        if (IsShuffleEnabled)
        {
            var shuffled = _originalTracks.ToList();
            int n = shuffled.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                (shuffled[k], shuffled[n]) = (shuffled[n], shuffled[k]);
            }

            if (keepCurrent is not null && shuffled.Remove(keepCurrent))
                shuffled.Insert(0, keepCurrent);

            _playbackTracks.AddRange(shuffled);
        }
        else
        {
            _playbackTracks.AddRange(_originalTracks);
        }

        _currentIndex = keepCurrent is not null
            ? _playbackTracks.IndexOf(keepCurrent)
            : (_playbackTracks.Count > 0 ? 0 : -1);
    }
}
using System;
using System.Collections.Generic;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Player;

namespace MusicPlayer.Playback;

public sealed class PlaybackQueue : IPlaybackQueue
{
    private readonly List<AudioTrack> _originalTracks = [];
    private readonly HashSet<AudioTrack> _originalTrackSet = [];
    private readonly List<AudioTrack> _playbackTracks = [];

    private int _currentIndex = -1;

    public AudioTrack? CurrentTrack =>
        _currentIndex >= 0 && _currentIndex < _playbackTracks.Count
            ? _playbackTracks[_currentIndex]
            : null;

    public IReadOnlyList<AudioTrack> Tracks => _playbackTracks;

    public int CurrentIndex => _currentIndex;

    public bool HasNext => _playbackTracks.Count > 0 &&
        (RepeatMode != PlaybackRepeatMode.Off || _currentIndex < _playbackTracks.Count - 1);

    public bool HasPrevious => _playbackTracks.Count > 0 &&
        (RepeatMode != PlaybackRepeatMode.Off || _currentIndex > 0);

    public bool IsShuffleEnabled { get; private set; }
    public PlaybackRepeatMode RepeatMode { get; private set; } = PlaybackRepeatMode.Off;

    public void SetTracks(IReadOnlyCollection<AudioTrack> tracks, AudioTrack current)
    {
        _originalTracks.Clear();
        _originalTrackSet.Clear();

        foreach (AudioTrack track in tracks)
        {
            if (_originalTrackSet.Add(track))
            {
                _originalTracks.Add(track);
            }
        }

        RebuildPlaybackList(current);
    }

    public void Enqueue(AudioTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (_originalTrackSet.Add(track))
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

        bool removedFromOriginal = _originalTrackSet.Remove(track) && _originalTracks.Remove(track);
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

    public void MoveItem(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex ||
            oldIndex < 0 || oldIndex >= _playbackTracks.Count ||
            newIndex < 0 || newIndex >= _playbackTracks.Count)
        {
            return;
        }

        AudioTrack item = _playbackTracks[oldIndex];
        _playbackTracks.RemoveAt(oldIndex);
        _playbackTracks.Insert(newIndex, item);

        if (_currentIndex == oldIndex)
        {
            _currentIndex = newIndex;
        }
        else if (oldIndex < _currentIndex && newIndex >= _currentIndex)
        {
            _currentIndex--;
        }
        else if (oldIndex > _currentIndex && newIndex <= _currentIndex)
        {
            _currentIndex++;
        }
    }

    public bool JumpTo(int index)
    {
        if (index < 0 || index >= _playbackTracks.Count)
            return false;

        _currentIndex = index;
        return true;
    }

    public void Clear()
    {
        _originalTracks.Clear();
        _originalTrackSet.Clear();
        _playbackTracks.Clear();
        _currentIndex = -1;
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
        RebuildPlaybackList(CurrentTrack);
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

        if (_originalTracks.Count == 0)
        {
            _currentIndex = -1;
            return;
        }

        _playbackTracks.AddRange(_originalTracks);

        if (IsShuffleEnabled)
        {
            int startOffset = 0;

            // Перемещаем текущий трек в начало за O(1) через обмен, чтобы он не прерывался
            if (keepCurrent is not null)
            {
                int keepIdx = _playbackTracks.IndexOf(keepCurrent);
                if (keepIdx >= 0)
                {
                    (_playbackTracks[0], _playbackTracks[keepIdx]) = (_playbackTracks[keepIdx], _playbackTracks[0]);
                    startOffset = 1;
                }
            }

            // 1. Fisher-Yates на диапазоне [startOffset .. Count - 1]
            for (int i = _playbackTracks.Count - 1; i > startOffset; i--)
            {
                int j = Random.Shared.Next(startOffset, i + 1);
                (_playbackTracks[i], _playbackTracks[j]) = (_playbackTracks[j], _playbackTracks[i]);
            }

            // 2. Анти-кластеризация исполнителей: стараемся не ставить подряд треки одного исполнителя
            SpreadSameArtistTracks(startOffset);

            _currentIndex = 0;
            return;
        }

        if (keepCurrent is not null)
        {
            int idx = _playbackTracks.IndexOf(keepCurrent);
            _currentIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            _currentIndex = 0;
        }
    }

    private void SpreadSameArtistTracks(int startOffset)
    {
        int count = _playbackTracks.Count;
        if (count - startOffset < 3)
            return;

        for (int i = Math.Max(1, startOffset); i < count - 1; i++)
        {
            string prevArtist = _playbackTracks[i - 1].Artist;
            if (!string.Equals(_playbackTracks[i].Artist, prevArtist, StringComparison.OrdinalIgnoreCase))
                continue;

            for (int candidate = i + 1; candidate < count; candidate++)
            {
                if (!string.Equals(_playbackTracks[candidate].Artist, prevArtist, StringComparison.OrdinalIgnoreCase))
                {
                    (_playbackTracks[i], _playbackTracks[candidate]) = (_playbackTracks[candidate], _playbackTracks[i]);
                    break;
                }
            }
        }
    }
}
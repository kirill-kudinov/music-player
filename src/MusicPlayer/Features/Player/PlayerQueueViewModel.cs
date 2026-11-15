using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Features.Playlists;
using MusicPlayer.Playback;

namespace MusicPlayer.Features.Player;

public sealed partial class PlayerQueueViewModel(
    IPlaybackQueue queue,
    ITrackCoverExtractor coverExtractor) : ObservableObject
{
    private Action<AudioTrack>? _playTrackCallback;
    private Action<AudioTrack>? _removeTrackCallback;
    private Action? _queueReorderedCallback;

    public ObservableCollection<QueueTrackItemViewModel> Tracks { get; } = [];

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _trackCountText = "0 треков";

    [ObservableProperty]
    private string _totalDurationText = "0:00";

    public void AttachCallbacks(
        Action<AudioTrack> onPlayTrack,
        Action<AudioTrack> onRemoveTrack,
        Action onQueueReordered)
    {
        _playTrackCallback = onPlayTrack;
        _removeTrackCallback = onRemoveTrack;
        _queueReorderedCallback = onQueueReordered;
    }

    [RelayCommand]
    private void Open()
    {
        Refresh();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    private void PlayItem(QueueTrackItemViewModel? item)
    {
        if (item is null)
            return;

        int index = Tracks.IndexOf(item);
        if (index < 0 || !queue.JumpTo(index))
            return;

        _playTrackCallback?.Invoke(item.Track);
        UpdateCurrentMarkers();
    }

    [RelayCommand]
    private void RemoveItem(QueueTrackItemViewModel? item)
    {
        if (item is null)
            return;

        _removeTrackCallback?.Invoke(item.Track);
        Refresh();

        if (queue.Tracks.Count == 0)
        {
            IsOpen = false;
        }
    }

    public void MoveItem(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex ||
            oldIndex < 0 || oldIndex >= Tracks.Count ||
            newIndex < 0 || newIndex >= Tracks.Count)
        {
            return;
        }

        queue.MoveItem(oldIndex, newIndex);
        Tracks.Move(oldIndex, newIndex);
        UpdateCurrentMarkers();
        _queueReorderedCallback?.Invoke();
    }

    public void Refresh()
    {
        IReadOnlyList<AudioTrack> tracks = queue.Tracks;
        int currentIndex = queue.CurrentIndex;

        Tracks.Clear();
        TimeSpan totalDuration = TimeSpan.Zero;

        for (int i = 0; i < tracks.Count; i++)
        {
            AudioTrack track = tracks[i];
            totalDuration += track.Duration;
            ImageSource? cover = coverExtractor.ExtractCover(track.ContentUri);
            Tracks.Add(new QueueTrackItemViewModel(track, cover, i == currentIndex));
        }

        TrackCountText = PlaylistFormatting.FormatTrackCount(tracks.Count);
        TotalDurationText = FormatTotalDuration(totalDuration);
    }

    public void UpdateCurrentMarkers()
    {
        int currentIndex = queue.CurrentIndex;
        for (int i = 0; i < Tracks.Count; i++)
        {
            Tracks[i].IsCurrent = i == currentIndex;
        }
    }

    private static string FormatTotalDuration(TimeSpan total)
    {
        if (total.TotalHours >= 1.0)
        {
            int hours = (int)total.TotalHours;
            return $"{hours}:{total.Minutes:D2}:{total.Seconds:D2}";
        }

        int totalMinutes = (int)total.TotalMinutes;
        return $"{totalMinutes}:{total.Seconds:D2}";
    }
}

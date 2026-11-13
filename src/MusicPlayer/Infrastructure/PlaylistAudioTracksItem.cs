using System.ComponentModel.DataAnnotations.Schema;

namespace MusicPlayer.Infrastructure;

public sealed class PlaylistAudioTracksItem
{
    [ForeignKey(nameof(PlaylistItem.Id))] public int PlaylistId { get; set; }
    [ForeignKey(nameof(AudioTrackItem.Id))] public int AudioTrackId { get; set; }
}
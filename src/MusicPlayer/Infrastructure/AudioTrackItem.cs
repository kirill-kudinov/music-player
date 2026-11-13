using System.ComponentModel.DataAnnotations.Schema;
using SQLite;

namespace MusicPlayer.Infrastructure;

public sealed class AudioTrackItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [ForeignKey(nameof(PlaylistItem.Id))] public int PlaylistId { get; set; }
    public string Title { get; set; }
    public string Artist { get; set; }
    public string Duration { get; set; }
    public bool IsFavorite { get; set; }
    public string Uri { get; set; }
    public string AudioFormat { get; set; }
}
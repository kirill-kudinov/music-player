using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MusicPlayer.Core.AudioTracks;
using MusicPlayer.Core.Primitives;

namespace MusicPlayer.Features.Importer;

public readonly record struct MediaScanProgress(int Processed, int Total);

public interface IMediaStoreAudioScanner
{
    event EventHandler<IReadOnlyList<AudioTrack>>? TracksScanned;

    IReadOnlyList<AudioTrack> CachedTracks { get; }

    Task<bool> HasReadAudioPermissionAsync();

    Task<Result<IReadOnlyList<AudioTrack>>> ScanDeviceAudioAsync(
        IProgress<MediaScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

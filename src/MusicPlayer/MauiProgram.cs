using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using MusicPlayer.Features.Importer;
using MusicPlayer.Features.Player;
using MusicPlayer.Features.Player.Animations;
using MusicPlayer.Features.Tracks;
using MusicPlayer.Playback;

namespace MusicPlayer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(false)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Roboto-Regular.ttf", "Roboto-Regular");
                fonts.AddFont("Roboto-Bold.ttf", "Roboto-Bold");
            });

#if ANDROID
        builder.Services.AddSingleton<IAudioPlayer, AndroidAudioPlayer>();
        builder.Services.AddSingleton<ITrackCoverExtractor, AndroidTrackCoverExtractor>();
#endif
        builder.Services.AddSingleton<IPlaybackQueue, PlaybackQueue>();
        builder.Services.AddSingleton<IPlayerTransitionService, PlayerTransitionService>();
        builder.Services.AddSingleton<TrackStorageSettings>();
        builder.Services.AddSingleton<TrackStorage>();
        builder.Services.AddSingleton<TrackPicker>();

        builder.Services.AddSingleton<PlayerViewModel>();
        builder.Services.AddTransient<PlayerPage>();

        builder.Services.AddSingleton<TracksViewModel>();
        builder.Services.AddSingleton<TracksPage>();

        builder.Services.AddTransient<ImportViewModel>();
        builder.Services.AddTransient<ImportPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
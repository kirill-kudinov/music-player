using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using MusicPlayer.Features.Player;
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
            .UseMauiCommunityToolkitMediaElement(false, null)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Roboto-Regular.ttf", "Roboto-Regular");
                fonts.AddFont("Roboto-Bold.ttf", "Roboto-Bold");
            });

#if ANDROID
        builder.Services.AddSingleton<IAudioPlayer, AndroidAudioPlayer>();
#endif
        builder.Services.AddSingleton<IPlaybackQueue, PlaybackQueue>();

        builder.Services.AddTransient<PlayerViewModel>();
        builder.Services.AddTransient<PlayerPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
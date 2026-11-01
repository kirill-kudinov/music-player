using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using CommunityToolkit.Maui;

namespace MusicPlayer;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            
        }).UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(false, null);
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
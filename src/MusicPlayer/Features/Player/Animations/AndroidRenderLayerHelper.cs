using Microsoft.Maui.Controls;

namespace MusicPlayer.Features.Player.Animations;

internal static class AndroidRenderLayerHelper
{
    public static void SetHardwareLayer(VisualElement element, bool enabled)
    {
#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.SetLayerType(
                enabled ? Android.Views.LayerType.Hardware : Android.Views.LayerType.None,
                null);
        }
#endif
    }
}

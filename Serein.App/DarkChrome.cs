using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Serein.App;
public static class DarkChrome
{
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) => Update(window);
    }
    public static void Update(Window window)
    {
        try { var handle = new WindowInteropHelper(window).Handle; if (handle == IntPtr.Zero) return; int enabled = Appearance.IsLight ? 0 : 1; DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int)); } catch { /* Older Windows keeps the standard title bar. */ }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MintADB.Wpf.Helpers;

/// <summary>
/// Clips the native HWND to a rounded rect — fixes square corners while dragging/resizing.
/// </summary>
public static class WindowRoundHelper
{
    public const int DefaultRadius = 26;

    private const int WmMoving = 0x0216;
    private const int WmSizing = 0x0214;
    private const int WmExitSizeMove = 0x0232;
    private const int WmWindowPosChanged = 0x0047;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void Attach(Window window, int radius = DefaultRadius)
    {
        void Refresh(object? _, EventArgs __) => Apply(window, radius);

        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            HwndSource.FromHwnd(hwnd)?.AddHook(
                (IntPtr _, int msg, IntPtr __, IntPtr ___, ref bool ____) =>
                    OnWindowMessage(window, radius, msg));

            Apply(window, radius);
        };

        window.Loaded += Refresh;
        window.ContentRendered += Refresh;
        window.SizeChanged += Refresh;
        window.StateChanged += Refresh;
        window.LocationChanged += Refresh;
    }

    private static IntPtr OnWindowMessage(Window window, int radius, int msg)
    {
        if (msg is WmMoving or WmSizing or WmExitSizeMove or WmWindowPosChanged)
            Apply(window, radius);
        return IntPtr.Zero;
    }

    public static void Apply(Window window, int radius = DefaultRadius)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (window.WindowState == WindowState.Maximized)
        {
            SetWindowRgn(hwnd, IntPtr.Zero, true);
            return;
        }

        int pref = DwmwcpRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref pref, sizeof(int));

        if (!GetClientRect(hwnd, out var rect)) return;

        int w = rect.Right - rect.Left;
        int h = rect.Bottom - rect.Top;
        if (w < 4 || h < 4) return;

        int r = Math.Clamp(radius, 8, Math.Min(w, h) / 2);

        // +1 ensures the bottom/right edge pixels are included in the region
        var rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, r, r);
        SetWindowRgn(hwnd, rgn, true);
    }
}
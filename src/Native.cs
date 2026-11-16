using System;
using System.Runtime.InteropServices;

namespace Launcher;

static class Native
{
    [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int l, r, t, b; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref MARGINS m);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint PrivateExtractIcons(string f, int i, int w, int h, IntPtr[] ph, uint[] pid, uint n, uint fl);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);

    // keeps a window out of Alt+Tab (tool window) and stops it taking focus
    public static void HideFromAltTab(IntPtr h)
    {
        const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_APPWINDOW = 0x40000;
        int ex = GetWindowLong(h, GWL_EXSTYLE);
        ex = (ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE) & ~WS_EX_APPWINDOW;
        SetWindowLong(h, GWL_EXSTYLE, ex);
    }

    // clips the window to a rounded rectangle (device pixels); DWM's own corners are only ~8px
    public static void ClearRegion(IntPtr h) => SetWindowRgn(h, IntPtr.Zero, true);

    public static void RoundRegion(IntPtr h, int w, int ht, int radiusPx)
    {
        if (w <= 0 || ht <= 0) return;
        var rgn = CreateRoundRectRgn(0, 0, w, ht, radiusPx * 2, radiusPx * 2);
        SetWindowRgn(h, rgn, true);     // the system owns the region after this call
    }

    public static bool KeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    // type: 1 = none (solid), 2 = Mica, 3 = Acrylic (glass/blur), 4 = Mica Alt
    public static void Backdrop(IntPtr h, int type, bool dark)
    {
        int d = dark ? 1 : 0;  DwmSetWindowAttribute(h, 20, ref d, 4);   // immersive dark mode
        int c = 2;             DwmSetWindowAttribute(h, 33, ref c, 4);   // Windows rounds + anti-aliases the corners (same in Solid and Mica)
        int bc = -2;           DwmSetWindowAttribute(h, 34, ref bc, 4);  // no DWM border
        int t = type;          DwmSetWindowAttribute(h, 38, ref t, 4);   // system backdrop
        var m = new MARGINS();
        int v = (type == 1) ? 0 : -1;
        m.l = v; m.r = v; m.t = v; m.b = v;
        DwmExtendFrameIntoClientArea(h, ref m);
    }
}

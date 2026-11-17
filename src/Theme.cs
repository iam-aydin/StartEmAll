using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Launcher;

sealed class Pal
{
    public bool Dark;
    public Color Fg, Dim, Fill, Hover, Sel, BtnEdge, PanelEdge;
    public byte Tr, Tg, Tb, A0, A1, E0, E1, E2, SpotA;
    public byte[] Base = new byte[3];
}

static class Theme
{
    static Color C(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
    public static SolidColorBrush Br(Color c) => new SolidColorBrush(c);

    // Mica Alt needs Windows 11 22H2+
    public static bool BuildOld => Environment.OSVersion.Version.Build < 22621;

    public static Pal Get()
    {
        if (Config.Cur.Theme == "Light")
        {
            return new Pal
            {
                Dark = false, Fg = C(0xFF, 0, 0, 0), Dim = C(0xFF, 0x66, 0x66, 0x66), Fill = C(0x10, 0, 0, 0),
                Hover = C(0x20, 0, 0, 0), Sel = C(0x2C, 0, 0, 0), BtnEdge = C(0x26, 0, 0, 0), PanelEdge = C(0x30, 0, 0, 0),
                Tr = 0, Tg = 0, Tb = 0, A0 = 14, A1 = 30, E0 = 0x30, E1 = 0x0C, E2 = 0x14, SpotA = 18,
                Base = new byte[] { 243, 243, 246 }
            };
        }
        return new Pal
        {
            Dark = true, Fg = C(0xFF, 255, 255, 255), Dim = C(0xFF, 0xA6, 0xA6, 0xA6), Fill = C(0x18, 255, 255, 255),
            Hover = C(0x2E, 255, 255, 255), Sel = C(0x3A, 255, 255, 255), BtnEdge = C(0x30, 255, 255, 255), PanelEdge = C(0x40, 255, 255, 255),
            Tr = 255, Tg = 255, Tb = 255, A0 = 20, A1 = 38, E0 = 0x40, E1 = 0x10, E2 = 0x1A, SpotA = 30,
            Base = new byte[] { 28, 28, 32 }
        };
    }

    public static int BackdropType()
    {
        if (BuildOld) return 1;
        return Config.Cur.Backdrop == "MicaAlt" ? 4 : 1;
    }

    // the user's Windows accent colour (Settings > Personalisation > Colours)
    public static Color Accent()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\DWM");
            if (k?.GetValue("AccentColor") is int abgr)
                return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));   // stored as 0xAABBGGRR
        }
        catch { }
        var g = SystemParameters.WindowGlassColor;
        return Color.FromRgb(g.R, g.G, g.B);
    }

    public static Color Tint(Pal pal)
    {
        byte r = pal.Base[0], g = pal.Base[1], b = pal.Base[2];
        byte a = 255;
        if (BackdropType() != 1) a = (byte)Math.Round(Config.Cur.Opacity * 2.55);
        return C(a, r, g, b);
    }

    // ---- smooth theme switching ----
    // Every colour the XAML looks up is ONE persistent SolidColorBrush per window; a theme change
    // animates that brush's Color instead of swapping brushes, so everything fades together.
    public const double WinRadius = 8;                 // keep in sync with CornerRadius in the window XAMLs
    const int FadeMs = 420;
    static readonly System.Collections.Generic.Dictionary<Window, bool> lastDark = new();
    public static readonly SolidColorBrush TileFg = new SolidColorBrush(Colors.White);   // shared by the card labels / glyphs

    static IEasingFunction Ease => new CubicEase { EasingMode = EasingMode.EaseInOut };

    // set a brush to `to`; when animate, fade from its current colour
    static void Put(ResourceDictionary r, string key, Color to, bool animate)
    {
        if (r[key] is not SolidColorBrush b || b.IsFrozen) { r[key] = new SolidColorBrush(to); return; }
        PutBrush(b, to, animate);
    }

    static void PutBrush(SolidColorBrush b, Color to, bool animate)
    {
        var from = b.Color;
        b.BeginAnimation(SolidColorBrush.ColorProperty, null);
        b.Color = to;
        if (animate && from != to)
            b.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(from, to, TimeSpan.FromMilliseconds(FadeMs)) { EasingFunction = Ease, FillBehavior = FillBehavior.Stop });
    }

    // sets the brushes the XAML styles look up via {DynamicResource ...}, then the DWM backdrop
    public static void Apply(Window? w)
    {
        if (w == null) return;
        var pal = Get();
        var r = w.Resources;
        bool changed = lastDark.TryGetValue(w, out bool was) && was != pal.Dark;
        lastDark[w] = pal.Dark;
        bool anim = changed && w.IsVisible;

        var tint = Tint(pal);
        var ac = Accent();
        double lum = (0.299 * ac.R + 0.587 * ac.G + 0.114 * ac.B) / 255.0;

        Put(r, "PanelEdge", pal.PanelEdge, anim);
        Put(r, "BtnEdge", pal.BtnEdge, anim);
        Put(r, "Fg", pal.Fg, anim);
        Put(r, "FgDim", pal.Dim, anim);
        Put(r, "Fill", pal.Fill, anim);
        Put(r, "FillHover", pal.Hover, anim);
        Put(r, "Sel", pal.Sel, anim);
        Put(r, "Accent", ac, false);
        Put(r, "AccentFg", lum > 0.5 ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255), false);
        Put(r, "Knob", Colors.White, false);                                   // slider / switch knobs: white in every theme
        Put(r, "PanelSolid", Color.FromRgb(tint.R, tint.G, tint.B), anim);     // opaque, for popup menus
        if (ReferenceEquals(w, Core.Main)) PutBrush(TileFg, pal.Fg, anim);

        var h = new WindowInteropHelper(w).Handle;
        bool mica = BackdropType() != 1;

        if (r["PanelTint"] is not SolidColorBrush pt || pt.IsFrozen) { r["PanelTint"] = new SolidColorBrush(tint); pt = (SolidColorBrush)r["PanelTint"]; if (h != IntPtr.Zero) Native.Backdrop(h, BackdropType(), pal.Dark); }
        else if (anim && mica && h != IntPtr.Zero)
        {
            // the Mica material itself flips instantly, so hide that flip: fade the panel to fully opaque in the NEW
            // colour, flip the backdrop while it is covered, then fade back down to the normal translucency
            var cover = C(255, pal.Base[0], pal.Base[1], pal.Base[2]);
            var from = pt.Color;
            pt.BeginAnimation(SolidColorBrush.ColorProperty, null);
            pt.Color = tint;
            var kf = new ColorAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(FadeMs + 160), FillBehavior = FillBehavior.Stop };
            kf.KeyFrames.Add(new EasingColorKeyFrame(from, KeyTime.FromPercent(0)));
            kf.KeyFrames.Add(new EasingColorKeyFrame(cover, KeyTime.FromPercent(0.45)) { EasingFunction = Ease });
            kf.KeyFrames.Add(new EasingColorKeyFrame(tint, KeyTime.FromPercent(1)) { EasingFunction = Ease });
            pt.BeginAnimation(SolidColorBrush.ColorProperty, kf);
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds((FadeMs + 160) * 0.45) };
            t.Tick += (s, e) => { t.Stop(); Native.Backdrop(h, BackdropType(), pal.Dark); };
            t.Start();
        }
        else
        {
            PutBrush(pt, tint, anim);
            if (h != IntPtr.Zero) Native.Backdrop(h, BackdropType(), pal.Dark);
        }

        if (h != IntPtr.Zero) Native.ClearRegion(h);     // no custom clip any more (it could not be anti-aliased)

        if (w is MainWindow cw) cw.UpdateCloseGlyph();

        // scroll bars are off unless the user enabled them
        var vis = Config.Cur.Scrollbars ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden;
        if (w is MainWindow mw && mw.Scroller != null) mw.Scroller.VerticalScrollBarVisibility = vis;
        if (w is SettingsWindow sw && sw.PageScroll != null) sw.PageScroll.VerticalScrollBarVisibility = vis;
    }

    // call in every window constructor
    public static void Init(Window w, bool draggable = true, bool hideFromAltTab = false)
    {
        w.SourceInitialized += (s, e) =>
        {
            var h = new WindowInteropHelper(w).Handle;
            var src = HwndSource.FromHwnd(h);
            if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
            if (hideFromAltTab) Native.HideFromAltTab(h);
            Apply(w);
        };

        // left-click on any empty space drags the window (buttons, sliders, text boxes handle the click themselves)
        if (draggable)
            w.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState != System.Windows.Input.MouseButtonState.Pressed) return;
                try { w.DragMove(); } catch { }
            };
    }
}

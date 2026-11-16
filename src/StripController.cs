using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Launcher;

// quick-launch strip at any screen edge: hold the trigger key with the cursor resting at the edge.
//
// The cursor is polled on its own background thread (not a DispatcherTimer), so the 3D card
// rendering on the UI thread can never starve it, and every monitor's edge counts, along the
// whole edge. The strip then opens on that monitor, centred on where the cursor is.
static class StripController
{
    public static volatile bool Shown;
    public static volatile bool Dragging;        // a card is being reordered: never auto-hide
    public static bool KeepOpen;                 // a rebuild must not hide / move the open strip
    public static int Count;
    public static double W = 98, H = 440;

    static readonly object gate = new();
    static Rectangle stripRect;                 // strip bounds in physical pixels (for the poll thread)
    static volatile bool showPending;
    static volatile bool entered;                // cursor has reached the strip since it appeared
    static Thread? poll;

    static double Scale() => (double)System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width / SystemParameters.PrimaryScreenWidth;

    static void OnUi(Action a) => System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, a);

    // size + orientation from the settings; parks the strip on the primary screen
    public static void Layout()
    {
        var cfg = Config.Cur;
        int n = cfg.Apps.Count(a => a.Strip);
        Count = n;
        double len = Math.Max(1, n) * 80 + 40;
        bool vert = cfg.StripSide == "Left" || cfg.StripSide == "Right";
        Core.Strip.TilesPanel.Orientation = vert ? Orientation.Vertical : Orientation.Horizontal;
        W = vert ? 98 : len;
        H = vert ? len : 98;
        Core.Strip.Width = W; Core.Strip.Height = H;
        var scr = System.Windows.Forms.Screen.PrimaryScreen!;
        if (KeepOpen && Shown) return;
        Place(scr, new System.Drawing.Point(scr.Bounds.X + scr.Bounds.Width / 2, scr.Bounds.Y + scr.Bounds.Height / 2));
        if (Shown) { Shown = false; Core.Strip.Hide(); }
    }

    // put the strip on this screen's edge, centred on the cursor along that edge
    static void Place(System.Windows.Forms.Screen scr, System.Drawing.Point cur)
    {
        double f = Scale();
        var wa = scr.WorkingArea;
        double l = wa.Left / f, t = wa.Top / f, r = wa.Right / f, b = wa.Bottom / f;
        double cx = cur.X / f, cy = cur.Y / f;
        // fixed spot: centred along the chosen edge of this screen's work area, wherever the cursor triggered it
        double midX = l + (r - l - W) / 2, midY = t + (b - t - H) / 2;
        switch (Config.Cur.StripSide)
        {
            case "Left":   Core.Strip.Left = l + 10;      Core.Strip.Top = midY; break;
            case "Top":    Core.Strip.Top = t + 10;       Core.Strip.Left = midX; break;
            case "Bottom": Core.Strip.Top = b - H - 10;   Core.Strip.Left = midX; break;
            default:       Core.Strip.Left = r - W - 10;  Core.Strip.Top = midY; break;
        }
        lock (gate)
            stripRect = new Rectangle((int)(Core.Strip.Left * f), (int)(Core.Strip.Top * f), (int)(W * f), (int)(H * f));
    }

    static void Show(System.Windows.Forms.Screen scr, System.Drawing.Point cur)
    {
        showPending = false;
        if (Shown || !Config.Cur.StripOn || Count < 1) return;
        Place(scr, cur);
        Shown = true; entered = false;
        var root = Core.Strip.Root;
        root.BeginAnimation(UIElement.OpacityProperty, null);
        root.Opacity = 0;
        Core.Strip.Show();
        Core.Strip.Topmost = false; Core.Strip.Topmost = true;     // stay above other topmost windows
        root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(180)));
    }

    static void Hide()
    {
        if (!Shown) return;
        Shown = false;
        var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
        a.Completed += (s, e) => { if (!Shown) Core.Strip.Hide(); };
        Core.Strip.Root.BeginAnimation(UIElement.OpacityProperty, a);
    }

    public static void Start()
    {
        poll = new Thread(PollLoop) { IsBackground = true, Name = "StripPoll", Priority = ThreadPriority.AboveNormal };
        poll.Start();
    }

    static bool InEdge(System.Windows.Forms.Screen scr, System.Drawing.Point p)
    {
        var b = scr.Bounds; const int e = 6;
        return Config.Cur.StripSide switch
        {
            "Left"   => p.X <= b.Left + e,
            "Top"    => p.Y <= b.Top + e,
            "Bottom" => p.Y >= b.Bottom - 1 - e,
            _        => p.X >= b.Right - 1 - e
        };
    }

    static void PollLoop()
    {
        var clock = Stopwatch.StartNew();
        long dwell = -1, leave = -1;
        while (true)
        {
            Thread.Sleep(12);
            try { Step(clock.ElapsedMilliseconds, ref dwell, ref leave); } catch { }
        }
    }

    static void Step(long now, ref long dwell, ref long leave)
    {
        var cfg = Config.Cur;
        if (!cfg.StripOn || Count < 1)
        {
            if (Shown) OnUi(Hide);
            dwell = -1; leave = -1; return;
        }
        var p = System.Windows.Forms.Cursor.Position;
        bool alt = Native.KeyDown(0x12);
        bool down = cfg.StripKey switch
        {
            "Ctrl"  => Native.KeyDown(0x11) && !alt,
            "Shift" => Native.KeyDown(0x10),
            _       => true
        };

        if (!Shown)
        {
            leave = -1;
            if (showPending) return;
            var scr = System.Windows.Forms.Screen.FromPoint(p);     // whichever monitor the cursor is on
            var b = scr.Bounds;
            const int e = 6;                                        // px band at the edge (the cursor stops at the edge, so this is easy to hit)
            bool inZone = cfg.StripSide switch
            {
                "Left"   => p.X <= b.Left + e,
                "Top"    => p.Y <= b.Top + e,
                "Bottom" => p.Y >= b.Bottom - 1 - e,
                _        => p.X >= b.Right - 1 - e
            };
            if (down && inZone)
            {
                if (dwell < 0) dwell = now;
                else if (now - dwell >= cfg.StripDwell)
                {
                    dwell = -1; showPending = true;
                    OnUi(() => Show(scr, p));
                }
            }
            else dwell = -1;
        }
        else
        {
            dwell = -1;
            if (Dragging) { leave = -1; entered = true; return; }
            Rectangle r; lock (gate) r = stripRect;
            r.Inflate(36, 36);
            bool slidingAlongEdge = !entered && InEdge(System.Windows.Forms.Screen.FromPoint(p), p);   // heading for the strip along the edge
            if (r.Contains(p)) { leave = -1; entered = true; }
            else if (slidingAlongEdge) leave = -1;
            else if (leave < 0) leave = now;
            else if (now - leave >= (entered ? 400 : 1500)) { leave = -1; OnUi(Hide); }
        }
    }
}

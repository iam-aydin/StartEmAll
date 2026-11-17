using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Launcher;

// Press a shortcut card and drag it: it follows the cursor, and dropping it on another card swaps places.
// A plain click (no movement) still launches the app.
static class Reorder
{
    const double StartDist = 8;           // px the cursor must move with the button down before it counts as a drag

    // set when a press turned into a drag, so the mouse-up does not also launch the app
    public static bool Suppress;

    sealed class Drag
    {
        public Tile T = null!;
        public List<Tile> Tiles = new();      // app cards in layout order
        public Point[] Pos = Array.Empty<Point>();   // top-left of each layout slot, panel coordinates
        public int[] Slot = Array.Empty<int>();      // Slot[i] = slot that card i currently sits in
        public int[] Occ = Array.Empty<int>();       // Occ[k]  = card sitting in slot k
        public int Me;
        public Vector Grab;
        public bool Swapped;
    }
    static Drag? D;
    // the strip cards take the strip's slots in the new order, every other card keeps its place
    static void ApplyStripOrder(List<AppEntry> order)
    {
        var all = Config.Ordered();
        int k = 0;
        for (int i = 0; i < all.Count; i++) if (all[i].Strip) all[i] = order[k++];
        Config.Cur.Apps.Clear();
        Config.Cur.Apps.AddRange(all);
        StripController.KeepOpen = true;
    }

    // right-click menu: move one strip shortcut to the first / last place
    public static void MoveInStrip(AppEntry app, bool toTop)
    {
        var order = Config.Ordered().Where(a => a.Strip).ToList();
        if (!order.Remove(app)) return;
        if (toTop) order.Insert(0, app); else order.Add(app);
        ApplyStripOrder(order);
        Config.Cur.Sort = "Custom";
        Core.Commit("tiles");
        StripController.KeepOpen = false;
    }

    static Panel PanelOf(Tile t) => t.IsMain ? Core.Main.TilesPanel : Core.Strip.TilesPanel;
    static DispatcherTimer? pump;         // keeps autoscroll going while the cursor rests near an edge

    public static void Attach(Tile t)
    {
        Point down = default, downPanel = default;
        bool pressed = false;

        t.Button.PreviewMouseLeftButtonDown += (s, e) =>
        {
            if (D != null) return;
            pressed = true;
            down = Mouse.GetPosition(t.Win);
            downPanel = Mouse.GetPosition(PanelOf(t));
        };
        t.Button.PreviewMouseMove += (s, e) =>
        {
            if (D != null) { if (D.T == t) Update(); return; }
            if (pressed && e.LeftButton == MouseButtonState.Pressed && (Mouse.GetPosition(t.Win) - down).Length > StartDist)
                Begin(t, downPanel);
        };
        t.Button.PreviewMouseLeftButtonUp += (s, e) => { pressed = false; Finish(t); };
        t.Button.LostMouseCapture += (s, e) => { pressed = false; Finish(t); };
    }

    static void Begin(Tile t, Point pressPoint)
    {
        var panel = PanelOf(t);
        var d = new Drag { T = t };
        foreach (UIElement c in panel.Children)
        {
            var tile = Core.Cards.FirstOrDefault(x => ReferenceEquals(x.Button, c));
            if (tile != null && tile.IsMain == t.IsMain && !tile.App.IsAdd) d.Tiles.Add(tile);
        }
        int n = d.Tiles.Count;
        d.Me = d.Tiles.IndexOf(t);
        if (d.Me < 0 || n < 2) return;

        d.Pos = new Point[n]; d.Slot = new int[n]; d.Occ = new int[n];
        for (int i = 0; i < n; i++)
        {
            d.Tiles[i].Button.RenderTransform = new TranslateTransform();
            d.Pos[i] = d.Tiles[i].Button.TranslatePoint(new Point(0, 0), panel);
            d.Slot[i] = i; d.Occ[i] = i;
        }
        d.Grab = pressPoint - d.Pos[d.Me];   // where on the card it was grabbed

        Suppress = true;
        D = d;
        if (!t.IsMain) StripController.Dragging = true;      // keep the strip open while a card is being moved
        Panel.SetZIndex(t.Button, 30);
        t.Button.CaptureMouse();
        pump = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        pump.Tick += (_, __) => Update();
        pump.Start();
        Update();
    }

    static void Update()
    {
        var d = D; if (d == null) return;
        var panel = PanelOf(d.T);
        var m = Mouse.GetPosition(panel);
        Panel.SetZIndex(d.T.Button, 30);

        // the dragged card sticks to the cursor
        var tt = (TranslateTransform)d.T.Button.RenderTransform;
        tt.X = m.X - d.Grab.X - d.Pos[d.Me].X;
        tt.Y = m.Y - d.Grab.Y - d.Pos[d.Me].Y;

        // cursor over another slot -> that card takes the slot the dragged card just left
        for (int k = 0; k < d.Occ.Length; k++)
        {
            if (k == d.Slot[d.Me]) continue;
            var b = d.Tiles[d.Occ[k]].Button;
            var r = new Rect(d.Pos[k].X - 6, d.Pos[k].Y - 6, b.ActualWidth + 12, b.ActualHeight + 12);
            if (!r.Contains(m)) continue;
            int other = d.Occ[k], mine = d.Slot[d.Me];
            d.Occ[mine] = other; d.Slot[other] = mine;
            d.Occ[k] = d.Me;     d.Slot[d.Me] = k;
            d.Swapped = true;
            var ot = (TranslateTransform)d.Tiles[other].Button.RenderTransform;
            Glide(ot, TranslateTransform.XProperty, d.Pos[mine].X - d.Pos[other].X);
            Glide(ot, TranslateTransform.YProperty, d.Pos[mine].Y - d.Pos[other].Y);
            break;
        }

        // autoscroll when the cursor is near the top / bottom of the box
        if (!d.T.IsMain) return;
        var sc = Core.Main.Scroller;
        double y = Mouse.GetPosition(sc).Y, edge = 44;
        if (y < edge) Renderer.ScrollBy(-(edge - Math.Max(y, 0)) * 0.6);
        else if (y > sc.ActualHeight - edge) Renderer.ScrollBy((Math.Min(y, sc.ActualHeight) - (sc.ActualHeight - edge)) * 0.6);
    }

    static void Glide(Animatable target, DependencyProperty prop, double to)
    {
        target.BeginAnimation(prop, new DoubleAnimation(to, TimeSpan.FromMilliseconds(170))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    static void Finish(Tile t)
    {
        var d = D;
        if (d == null || d.T != t) return;
        D = null;
        StripController.Dragging = false;
        pump?.Stop(); pump = null;
        t.Button.ReleaseMouseCapture();
        if (!d.Swapped) { foreach (var x in d.Tiles) x.Button.RenderTransform = Transform.Identity; return; }
        var order = d.Occ.Select(i => d.Tiles[i].App).ToList();     // slot order = new order
        if (t.IsMain)
        {
            Config.Cur.Apps.Clear();
            Config.Cur.Apps.AddRange(order);
        }
        else
        {
            ApplyStripOrder(order);
        }
        Config.Cur.Sort = "Custom";
        Core.Commit("tiles");                                       // saves settings.json and rebuilds the cards (clears the drag transforms)
        StripController.KeepOpen = false;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Launcher;

// A selection highlight that glides between radio buttons (segmented pills, side navigation) instead of snapping,
// while each item's text cross-fades between its normal and its "selected" colour.
sealed class SlideSel
{
    const int Ms = 240;
    readonly Grid host;
    readonly Border ind;
    readonly Thickness inset;
    readonly List<(RadioButton rb, UIElement sel)> items = new();
    bool placed;

    public SlideSel(Grid host, double radius, Thickness inset)
    {
        this.host = host; this.inset = inset;
        ind = new Border
        {
            CornerRadius = new CornerRadius(radius), HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Visibility = Visibility.Hidden
        };
        ind.SetResourceReference(Border.BackgroundProperty, "Accent");
        host.Children.Insert(0, ind);
        host.SizeChanged += (s, e) => Move(false);
        host.Loaded += (s, e) => Move(false);
    }

    // content: builds the item's visual in a given foreground resource key ("Fg" or "AccentFg")
    public static (Grid content, UIElement sel) Layers(Func<string, UIElement> make)
    {
        var g = new Grid();
        g.Children.Add(make("Fg"));
        var sel = make("AccentFg");
        sel.Opacity = 0; sel.IsHitTestVisible = false;
        g.Children.Add(sel);
        return (g, sel);
    }

    public void Add(RadioButton rb, UIElement sel)
    {
        items.Add((rb, sel));
        rb.Checked += (s, e) => Move(true);
    }

    static void Fade(UIElement el, double to, bool animate)
    {
        el.BeginAnimation(UIElement.OpacityProperty, null);
        double from = el.Opacity;
        el.Opacity = to;
        if (animate && from != to)
            el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(Ms)) { FillBehavior = FillBehavior.Stop });
    }

    public void Move(bool animate)
    {
        foreach (var it in items)
        {
            bool on = it.rb.IsChecked == true;
            Fade(it.sel, on ? 1 : 0, animate && placed);
            if (it.sel is FrameworkElement fe && fe.Parent is Panel pn && pn.Children.Count > 0) Fade(pn.Children[0], on ? 0 : 1, animate && placed);
        }
        var cur = items.FirstOrDefault(i => i.rb.IsChecked == true);
        if (cur.rb == null || cur.rb.ActualWidth <= 0 || !host.IsLoaded) return;

        var pt = cur.rb.TranslatePoint(new Point(0, 0), host);
        double l = pt.X + inset.Left, t = pt.Y + inset.Top;
        double w = Math.Max(0, cur.rb.ActualWidth - inset.Left - inset.Right), h = Math.Max(0, cur.rb.ActualHeight - inset.Top - inset.Bottom);

        bool doAnim = animate && placed;
        var oldM = ind.Margin; double oldW = double.IsNaN(ind.Width) ? w : ind.Width, oldH = double.IsNaN(ind.Height) ? h : ind.Height;
        ind.BeginAnimation(FrameworkElement.MarginProperty, null);
        ind.BeginAnimation(FrameworkElement.WidthProperty, null);
        ind.BeginAnimation(FrameworkElement.HeightProperty, null);
        ind.Margin = new Thickness(l, t, 0, 0); ind.Width = w; ind.Height = h;
        ind.Visibility = Visibility.Visible;
        if (doAnim)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var d = TimeSpan.FromMilliseconds(Ms);
            ind.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation(oldM, ind.Margin, d) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            ind.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(oldW, w, d) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            ind.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(oldH, h, d) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        }
        placed = true;
    }
}

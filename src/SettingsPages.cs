using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Launcher;

// builds the settings window pages in code (add a new setting = add one New-Row line here)
static class SettingsPages
{
    static SettingsWindow W = null!;

    static Style St(string key) => (Style)System.Windows.Application.Current.FindResource(key);

    // ------------------------------ builders ------------------------------
    static TextBlock Text(string text, double size, FontWeight? weight, bool dim)
    {
        var t = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        if (weight != null) t.FontWeight = weight.Value;
        t.SetResourceReference(TextBlock.ForegroundProperty, dim ? "FgDim" : "Fg");
        return t;
    }

    // ---- layout, after macOS System Settings: grouped rounded cards, rows split by hairlines, generous padding ----
    static Border Group(string? header, params UIElement[] rows)
    {
        var bd = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 26) };
        bd.SetResourceReference(Border.BackgroundProperty, "Fill");
        bd.SetResourceReference(Border.BorderBrushProperty, "BtnEdge");
        var sp = new StackPanel();
        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0)
            {
                var line = new Border { Height = 1, Margin = new Thickness(16, 0, 0, 0) };
                line.SetResourceReference(Border.BackgroundProperty, "BtnEdge");
                sp.Children.Add(line);
            }
            sp.Children.Add(rows[i]);
        }
        bd.Child = sp;
        if (header == null) return bd;
        // header + group share one wrapper so the spacing stays even
        var wrap = new StackPanel();
        var ht = Text(header, 13, FontWeights.SemiBold, false);
        ht.Margin = new Thickness(6, 0, 0, 8);
        bd.Margin = new Thickness(0, 0, 0, 26);
        wrap.Children.Add(ht); wrap.Children.Add(bd);
        return new Border { Child = wrap };
    }

    static Border Row(string title, string? sub, FrameworkElement ctrl)
    {
        var bd = new Border { Padding = new Thickness(16, 13, 16, 13), MinHeight = 54, Background = Brushes.Transparent };
        var dock = new DockPanel { LastChildFill = true };
        ctrl.VerticalAlignment = VerticalAlignment.Center;
        ctrl.Margin = new Thickness(24, 0, 0, 0);
        DockPanel.SetDock(ctrl, Dock.Right);
        dock.Children.Add(ctrl);
        var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(Text(title, 14, null, false));
        if (sub != null) { var st = Text(sub, 12, null, true); st.Margin = new Thickness(0, 3, 0, 0); sp.Children.Add(st); }
        dock.Children.Add(sp);
        bd.Child = dock;
        return bd;
    }

    static FrameworkElement Seg((string v, string label)[] opts, string cur, Action<string> cb)
    {
        var host = new Grid();
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        host.Children.Add(sp);
        var slide = new SlideSel(host, 16, new Thickness(0));
        string grp = Guid.NewGuid().ToString();
        foreach (var o in opts)
        {
            var (content, sel) = SlideSel.Layers(key => { var t = new TextBlock { Text = o.label, HorizontalAlignment = HorizontalAlignment.Center }; t.SetResourceReference(TextBlock.ForegroundProperty, key); return t; });
            var rb = new RadioButton { Style = St("Seg"), GroupName = grp, Content = content };
            rb.IsChecked = (o.v == cur);
            string v = o.v;
            rb.Checked += (s, e) => cb(v);
            slide.Add(rb, sel);
            sp.Children.Add(rb);
        }
        var track = new Border { CornerRadius = new CornerRadius(18), Padding = new Thickness(2), Child = host };
        track.SetResourceReference(Border.BackgroundProperty, "Sel");
        return track;
    }

    static ToggleButton Switch(bool on, Action<bool> cb)
    {
        var t = new ToggleButton { Style = St("Switch"), IsChecked = on };
        t.Click += (s, e) => cb(t.IsChecked == true);
        return t;
    }

    static StackPanel Slide(double min, double max, double val, double step, string fmt, Action<double> cb)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var sl = new Slider { Style = St("Slide"), Minimum = min, Maximum = max, Value = val, TickFrequency = step, IsSnapToTickEnabled = true };
        var lbl = Text(val.ToString(fmt, CultureInfo.InvariantCulture), 12, null, true);
        lbl.Width = 44; lbl.TextAlignment = TextAlignment.Right; lbl.Margin = new Thickness(12, 0, 0, 0); lbl.VerticalAlignment = VerticalAlignment.Center;
        sl.ValueChanged += (s, e) =>
        {
            lbl.Text = sl.Value.ToString(fmt, CultureInfo.InvariantCulture);
            cb(sl.Value);
        };
        sp.Children.Add(sl); sp.Children.Add(lbl);
        return sp;
    }

    static TextBox Field(string text, double width = 0)
    {
        var tb = new TextBox { Style = St("Field"), Text = text };
        if (width > 0) tb.Width = width;
        return tb;
    }

    static Button TxtBtn(string text, Action cb)
    {
        var b = new Button { Style = St("TxtBtn"), Content = text };
        b.Click += (s, e) => cb();
        return b;
    }

    static Button GlyphBtn(int glyph, Action cb)
    {
        var t = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Text = ((char)glyph).ToString() };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        var b = new Button { Style = St("TxtBtn"), Content = t, Padding = new Thickness(0), Width = 30, Height = 28 };
        b.Click += (s, e) => cb();
        return b;
    }

    static void SetStartup(bool on)
    {
        string lnk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Launcher.lnk");
        try
        {
            if (on)
            {
                string exe = Environment.ProcessPath ?? "";
                if (exe == "") return;
                dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                dynamic s = sh.CreateShortcut(lnk);
                s.TargetPath = exe;
                s.WorkingDirectory = System.IO.Path.GetDirectoryName(exe);
                s.Save();
            }
            else if (System.IO.File.Exists(lnk)) System.IO.File.Delete(lnk);
        }
        catch { }
    }

    // ------------------------------ pages ------------------------------
    public static void Build(string key) => Build(W, key);

    public static void Build(SettingsWindow win, string key)
    {
        W = win;
        var h = W.PageHost;
        h.Children.Clear();
        var titles = new System.Collections.Generic.Dictionary<string, string>
            { ["appearance"] = "Appearance", ["strip"] = "Side strip", ["general"] = "General" };
        var tt = Text(titles[key], 24, FontWeights.SemiBold, false); tt.Margin = new Thickness(4, 8, 0, 22);
        h.Children.Add(tt);
        var c = Config.Cur;

        switch (key)
        {
            case "appearance":
                h.Children.Add(Group(null,
                    Row("Theme", "Dark or light interface", Seg(new[] { ("Dark", "Dark"), ("Light", "Light") }, c.Theme, v =>
                        { if (Config.Cur.Theme != v) { Config.Cur.Theme = v; Core.Commit("look", "tiles"); } })),
                    Row("Backdrop", "Solid or Mica Alt (needs Windows 11 22H2+)",
                        Seg(new[] { ("Solid", "Solid"), ("MicaAlt", "Mica Alt") }, c.Backdrop, v =>
                        { if (Config.Cur.Backdrop != v) { Config.Cur.Backdrop = v; Core.Commit("look"); } })),
                    Row("Window opacity", "How strongly the panel colour covers the backdrop (not used in Solid)",
                        Slide(0, 100, c.Opacity, 1, "F0", v => { Config.Cur.Opacity = Math.Round(v); Core.Commit("look"); })),
                    Row("Scroll bars", "Show scroll bars in the launcher and settings (scrolling with the wheel always works)",
                        Switch(c.Scrollbars, on => { Config.Cur.Scrollbars = on; Core.Commit("look"); }))));
                break;

            case "strip":
                h.Children.Add(Group(null,
                    Row("Side strip", "Quick-launch bar that appears at a screen edge",
                        Switch(c.StripOn, on => { Config.Cur.StripOn = on; Config.Save(); }))));
                h.Children.Add(Group("Position and trigger",
                    Row("Screen edge", "Where the strip appears",
                        Seg(new[] { ("Right", "Right"), ("Left", "Left"), ("Top", "Top"), ("Bottom", "Bottom") }, c.StripSide, v =>
                        { if (Config.Cur.StripSide != v) { Config.Cur.StripSide = v; Core.Commit("strip"); } })),
                    Row("Trigger key", "Hold it while the cursor rests at the edge",
                        Seg(new[] { ("Ctrl", "Ctrl"), ("Shift", "Shift"), ("None", "None") }, c.StripKey, v =>
                        { Config.Cur.StripKey = v; Config.Save(); })),
                    Row("Dwell time", "Milliseconds the cursor must rest at the edge",
                        Slide(100, 1000, c.StripDwell, 50, "F0", v => { Config.Cur.StripDwell = Math.Round(v); Config.Save(); }))));
                break;

            case "general":
                string lnk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Launcher.lnk");
                h.Children.Add(Group("Window",
                    Row("Close button", "What the X in the launcher does",
                        Seg(new[] { ("Hide", "Hide to tray"), ("Quit", "Quit") }, c.CloseAction, v => { Config.Cur.CloseAction = v; Config.Save(); })),
                    Row("Hide after launching", "Close the launcher when a shortcut is started",
                        Switch(c.HideAfterLaunch, on => { Config.Cur.HideAfterLaunch = on; Config.Save(); }))));
                h.Children.Add(Group("System",
                    Row("Start with Windows", null, Switch(System.IO.File.Exists(lnk), on => SetStartup(on)))));
                h.Children.Add(Group("Data",
                    Row("Settings file", "settings.json, next to the app", TxtBtn("Show in folder", () =>
                    {
                        Config.Save();
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{Config.Path_}\"") { UseShellExecute = true });
                    })),
                    Row("Reset", "Restore every setting and the default shortcuts", TxtBtn("Reset all settings", () =>
                    {
                        if (MessageBox.Show("Reset all settings to defaults?", "Launcher", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            Config.Cur = Config.Defaults();
                            Core.Commit("look", "tiles");
                            Build("general");
                        }
                    }))));
                break;
        }
        W.PageScroll.ScrollToTop();
    }
}

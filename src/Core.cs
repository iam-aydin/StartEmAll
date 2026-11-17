using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Launcher;

// shared state + app lifetime: windows, tray icon, rebuild / commit
static class Core
{
    public static MainWindow Main = null!;
    public static StripWindow Strip = null!;
    public static SettingsWindow? Sets;
    public static bool Quitting;
    public static readonly List<Tile> Cards = new();
    static System.Windows.Forms.NotifyIcon? ni;

    public static void Start()
    {
        Config.Load();

        Main = new MainWindow();
        Strip = new StripWindow();
        Theme.Apply(Main); Theme.Apply(Strip);
        Rebuild();

        // ignore the Alt key completely (no system menu, no focus flash)
        KeyEventHandler altBlock = (s, e) =>
        {
            if (e.Key == Key.System || e.Key == Key.LeftAlt || e.Key == Key.RightAlt || e.Key == Key.F10) e.Handled = true;
        };
        foreach (var w in new Window[] { Main, Strip }) { w.PreviewKeyDown += altBlock; w.PreviewKeyUp += altBlock; }

        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            Main.Dispatcher.BeginInvoke(new Action(() => { foreach (var w in new Window?[] { Main, Strip, Sets }) Theme.Apply(w); }));

        Main.ContentRendered += (s, e) => MeasureTiles();
        Renderer.Start();

        Main.CloseBtn.Click += (s, e) => { if (Config.Cur.CloseAction == "Quit") Quit(); else Main.Hide(); };
        Main.GearBtn.Click += (s, e) => ShowSettings(null);
        Main.Closing += (s, e) => { if (!Quitting) { e.Cancel = true; Main.Hide(); } };

        // tray icon: close hides here, double-click reopens
        ni = new System.Windows.Forms.NotifyIcon
        {
            Icon = AppIcon(),
            Text = "StartEmAll",
            Visible = true,
            ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip()
        };
        ni.DoubleClick += (s, e) => { Main.Show(); Main.Activate(); };
        ni.ContextMenuStrip.Items.Add("Open", null, (s, e) => { Main.Show(); Main.Activate(); });
        ni.ContextMenuStrip.Items.Add("Settings", null, (s, e) => ShowSettings(null));
        ni.ContextMenuStrip.Items.Add("Quit", null, (s, e) => Quit());

        StripController.Start();
        Main.Show();
    }

    // the exe's own icon (app.ico), falling back to a stock one
    static System.Drawing.Icon AppIcon()
    {
        try { var i = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? ""); if (i != null) return i; } catch { }
        return System.Drawing.SystemIcons.Application;
    }

    public static void Quit()
    {
        Quitting = true;
        if (ni != null) { ni.Visible = false; ni.Dispose(); }
        System.Windows.Application.Current.Shutdown();
    }

    public static void ShowSettings(string? page)
    {
        Sets ??= new SettingsWindow();
        Theme.Apply(Sets);
        Sets.Show(); Sets.Activate();
        if (page != null) Sets.SelectPage(page);
    }

    public static void MeasureTiles()
    {
        var tp = Main.TilesPanel;
        tp.UpdateLayout();
        double W = tp.ActualWidth;
        if (W <= 0) return;
        foreach (var t in Cards)
        {
            if (!t.IsMain) continue;
            var pt = t.Button.TranslatePoint(new Point(0, 0), tp);
            t.By = pt.Y;
            t.Bx = ((pt.X + t.Button.ActualWidth / 2) - W / 2) / (W / 2);
        }
        Renderer.LastOff = -1.0;
    }

    public static void Rebuild()
    {
        Cards.Clear();
        Main.TilesPanel.Children.Clear();
        Strip.TilesPanel.Children.Clear();
        var ordered = Config.Ordered();
        Main.SortText.Text = Config.SortLabel();
        foreach (var a in ordered) Main.TilesPanel.Children.Add(new Tile(Main, a, 120, true).Button);
        var add = new AppEntry { N = "Add", G = 0xE710, IsAdd = true };
        Main.TilesPanel.Children.Add(new Tile(Main, add, 120, true).Button);
        foreach (var a in ordered.Where(x => x.Strip)) Strip.TilesPanel.Children.Add(new Tile(Strip, a, 68, false).Button);
        StripController.Layout();
        if (Main.IsLoaded) Main.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(MeasureTiles));
    }

    // what: "look" (colors/backdrop), "tiles" (rebuild cards), "strip" (re-layout strip)
    public static void Commit(params string[] what)
    {
        Config.Save();
        if (what.Contains("look")) foreach (var w in new Window?[] { Main, Strip, Sets }) Theme.Apply(w);
        if (what.Contains("tiles")) Rebuild();
        if (what.Contains("strip")) StripController.Layout();
    }
}

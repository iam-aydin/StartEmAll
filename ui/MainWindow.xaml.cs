using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Launcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Theme.Apply(this);          // brushes first, so DynamicResource finds them
        InitializeComponent();
        Theme.Init(this);

        SortBtn.Click += (s, e) => { BuildSortMenu(); SortPop.IsOpen = true; };

        // drop .lnk / .exe (or any file) anywhere on the window to add it as a shortcut
        AllowDrop = true;
        PreviewDragOver += (s, e) =>
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        PreviewDrop += (s, e) =>
        {
            e.Handled = true;
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            int n = 0;
            foreach (var f in files)
                if (File.Exists(f)) { AppLauncher.AddPath(f); n++; }
            if (n > 0) Core.Commit("tiles");
        };
    }

    // arrow when the button hides to the tray (default), X when it quits the app
    public void UpdateCloseGlyph()
    {
        if (CloseGlyph == null) return;
        bool quit = Config.Cur.CloseAction == "Quit";
        CloseGlyph.Text = quit ? "\uE8BB" : "\uE70D";
        CloseGlyph.FontSize = quit ? 14 : 13;
    }

    static readonly (string key, string label)[] SortModes =
    {
        ("NameAsc", "Name  \u2193   A \u2192 Z"), ("NameDesc", "Name  \u2191   Z \u2192 A"),
        ("Newest", "Newest first"), ("Oldest", "Oldest first"), ("Custom", "Personalized")
    };

    // one row of a popup menu: check mark column + label
    Button MakeItem(string label, bool ticked, Action click, Popup pop)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var tick = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12, Text = "\uE73E",
            VerticalAlignment = VerticalAlignment.Center, Visibility = ticked ? Visibility.Visible : Visibility.Hidden };
        tick.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        var txt = new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        txt.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        Grid.SetColumn(txt, 1);
        row.Children.Add(tick); row.Children.Add(txt);
        var b = new Button { Style = (Style)FindResource("MenuItemBtn"), Content = row };
        b.Click += (s, e) => { pop.IsOpen = false; click(); };
        return b;
    }

    void BuildSortMenu()
    {
        SortItems.Children.Clear();
        foreach (var (key, label) in SortModes)
        {
            string k = key;
            SortItems.Children.Add(MakeItem(label, Config.Cur.Sort == k, () =>
            {
                if (Config.Cur.Sort != k) { Config.Cur.Sort = k; Core.Commit("tiles"); }
            }, SortPop));
        }
    }

    // right-click on a shortcut card
    public void ShowTileMenu(AppEntry app)
    {
        TileItems.Children.Clear();
        TileItems.Children.Add(MakeItem("Show in side strip", app.Strip, () => { app.Strip = !app.Strip; Core.Commit("tiles"); }, TileMenu));
        TileItems.Children.Add(MakeItem("Rename\u2026", false, () => Rename(app), TileMenu));
        TileItems.Children.Add(MakeItem("Change icon\u2026", false, () => ChangeIcon(app), TileMenu));
        if (!string.IsNullOrEmpty(app.Icon))
            TileItems.Children.Add(MakeItem("Reset icon", false, () => { IconStore.Delete(app.Icon); app.Icon = ""; Core.Commit("tiles"); }, TileMenu));
        TileItems.Children.Add(MakeItem("Remove shortcut", false, () =>
        {
            IconStore.Delete(app.Icon);
            Config.Cur.Apps.Remove(app);
            Core.Commit("tiles");
        }, TileMenu));
        TileMenu.IsOpen = true;
    }

    static void Rename(AppEntry app)
    {
        var name = PromptBox.Show(Core.Main, "Rename shortcut", app.N);
        if (string.IsNullOrWhiteSpace(name)) return;
        app.N = name.Trim();
        Core.Commit("tiles");
    }

    static void ChangeIcon(AppEntry app)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = IconStore.Filter, Title = $"Icon for {app.N}" };
        if (dlg.ShowDialog() != true) return;
        var saved = IconStore.Import(dlg.FileName);       // images are resized to max 256 px; exe / dll / lnk give their own icon
        if (saved == null) { MessageBox.Show("That image could not be read.", "StartEmAll", MessageBoxButton.OK); return; }
        IconStore.Delete(app.Icon);
        app.Icon = saved;
        Core.Commit("tiles");
    }
}

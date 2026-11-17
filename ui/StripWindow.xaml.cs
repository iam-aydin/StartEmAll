using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Launcher;

public partial class StripWindow : Window
{
    public StripWindow()
    {
        Theme.Apply(this);
        InitializeComponent();
        Theme.Init(this, draggable: false, hideFromAltTab: true);
        TileMenu.Closed += (s, e) => StripController.MenuOpen = false;
    }

    Button MakeItem(string label, Action click)
    {
        var txt = new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
        txt.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        var b = new Button { Style = (Style)FindResource("MenuItemBtn"), Content = txt };
        b.Click += (s, e) => { TileMenu.IsOpen = false; click(); };
        return b;
    }

    // right-click on a strip shortcut
    public void ShowTileMenu(AppEntry app)
    {
        TileItems.Children.Clear();
        TileItems.Children.Add(MakeItem("Move to top", () => Reorder.MoveInStrip(app, true)));
        TileItems.Children.Add(MakeItem("Move to end", () => Reorder.MoveInStrip(app, false)));
        StripController.MenuOpen = true;          // keep the strip open while the menu is showing
        TileMenu.IsOpen = true;
    }
}

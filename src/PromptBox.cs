using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Launcher;

// small themed "type a text" dialog (used by Rename)
static class PromptBox
{
    public static string? Show(Window owner, string title, string initial)
    {
        var dlg = new Window
        {
            Title = title, Width = 340, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None,
            Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false,
            Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"), UseLayoutRounding = true
        };
        Theme.Apply(dlg);       // brushes first, so DynamicResource finds them
        Theme.Init(dlg);

        string? result = null;
        var head = new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
        head.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        var tb = new TextBox { Style = (Style)System.Windows.Application.Current.FindResource("Field"), Text = initial, MaxLength = 80 };

        void Ok() { result = tb.Text; dlg.Close(); }
        Button Btn(string text, Action a)
        {
            var b = new Button { Style = (Style)System.Windows.Application.Current.FindResource("TxtBtn"), Content = text, Margin = new Thickness(8, 0, 0, 0) };
            b.Click += (s, e) => a();
            return b;
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        row.Children.Add(Btn("Cancel", () => dlg.Close()));
        row.Children.Add(Btn("OK", Ok));

        var sp = new StackPanel();
        sp.Children.Add(head); sp.Children.Add(tb); sp.Children.Add(row);
        var root = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(18), Child = sp };
        root.SetResourceReference(Border.BackgroundProperty, "PanelTint");
        root.SetResourceReference(Border.BorderBrushProperty, "PanelEdge");
        dlg.Content = root;

        dlg.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; Ok(); }
            else if (e.Key == Key.Escape) { e.Handled = true; dlg.Close(); }
        };
        dlg.Loaded += (s, e) => { dlg.Activate(); tb.Focus(); tb.SelectAll(); };
        dlg.ShowDialog();
        return result;
    }
}

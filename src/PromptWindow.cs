using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Launcher;

// small themed "type a text" dialog (used for renaming shortcuts)
sealed class PromptWindow : Window
{
    readonly TextBox box;
    string? result;

    PromptWindow(Window owner, string title, string current)
    {
        Owner = owner; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Background = Brushes.Transparent;
        ShowInTaskbar = false; Width = 380; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"); FontSize = 13; UseLayoutRounding = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Theme.Apply(this);

        var root = new Border { CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };
        root.SetResourceReference(Border.BackgroundProperty, "PanelTint");
        root.SetResourceReference(Border.BorderBrushProperty, "PanelEdge");
        var sp = new StackPanel { Margin = new Thickness(22, 18, 22, 20) };

        var t = new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold };
        t.SetResourceReference(TextBlock.ForegroundProperty, "Fg");
        box = new TextBox { Text = current, Style = (Style)System.Windows.Application.Current.FindResource("Field"), FontSize = 14, Margin = new Thickness(0, 14, 0, 18) };

        var ok = new Button { Style = (Style)System.Windows.Application.Current.FindResource("TxtBtn"), Content = "Save", MinWidth = 76 };
        var cancel = new Button { Style = (Style)System.Windows.Application.Current.FindResource("TxtBtn"), Content = "Cancel", MinWidth = 76 };
        ok.Click += (s, e) => Accept();
        cancel.Click += (s, e) => Close();
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(cancel); row.Children.Add(ok);

        sp.Children.Add(t); sp.Children.Add(box); sp.Children.Add(row);
        root.Child = sp;
        Content = root;

        Theme.Init(this);
        PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
            else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        Loaded += (s, e) => { box.Focus(); box.SelectAll(); };
    }

    void Accept() { result = box.Text; Close(); }

    public static string? Ask(Window owner, string title, string current)
    {
        var w = new PromptWindow(owner, title, current);
        w.ShowDialog();
        return w.result;
    }
}

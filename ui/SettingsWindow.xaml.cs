using System.Windows;
using System.Windows.Controls;

namespace Launcher;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        Theme.Apply(this);
        InitializeComponent();
        Theme.Init(this);

        SClose.Click += (s, e) => Hide();
        Closing += (s, e) => { if (!Core.Quitting) { e.Cancel = true; Hide(); } };

        // side navigation, built here so the highlight can glide between entries
        var slide = new SlideSel(NavHost, 16, new Thickness(0, 2, 0, 2));
        foreach (var (tag, glyph, label) in new[] { ("appearance", "\uE771", "Appearance"), ("strip", "\uE700", "Side strip"), ("general", "\uE713", "General") })
        {
            var (content, sel) = SlideSel.Layers(key =>
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var g = new TextBlock { FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 15, Text = glyph, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
                g.SetResourceReference(TextBlock.ForegroundProperty, key); t.SetResourceReference(TextBlock.ForegroundProperty, key);
                row.Children.Add(g); row.Children.Add(t);
                return row;
            });
            var rb = new RadioButton { GroupName = "nav", Style = (Style)FindResource("Nav"), Tag = tag, Content = content, IsChecked = tag == "appearance" };
            slide.Add(rb, sel);
            NavPanel.Children.Add(rb);
        }
        SettingsPages.Build(this, "appearance");
        foreach (RadioButton rb in NavPanel.Children)
            rb.Checked += (s, e) => SettingsPages.Build(this, (string)((RadioButton)s).Tag);
    }

    public void SelectPage(string page)
    {
        foreach (RadioButton rb in NavPanel.Children)
            if ((string)rb.Tag == page) rb.IsChecked = true;
    }
}

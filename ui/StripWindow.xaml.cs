using System.Windows;

namespace Launcher;

public partial class StripWindow : Window
{
    public StripWindow()
    {
        Theme.Apply(this);
        InitializeComponent();
        Theme.Init(this, draggable: false, hideFromAltTab: true);
    }
}

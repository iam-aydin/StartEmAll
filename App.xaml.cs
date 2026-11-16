using System;
using System.Threading;
using System.Windows;
using System.Windows.Media.Animation;

namespace Launcher;

public partial class App : System.Windows.Application
{
    static Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _single = new Mutex(true, "Launcher.SingleInstance", out bool first);
        if (!first) { Shutdown(); return; }

        try { Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new FrameworkPropertyMetadata { DefaultValue = 144 }); }
        catch { }

        Core.Start();
    }
}

using RulerOverlay.Helpers;
using RulerOverlay.Windows;
using System.Windows;

namespace RulerOverlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Enable per-monitor DPI awareness V2 for crisp rendering on high-DPI displays
        DpiHelper.EnablePerMonitorDpiAwarenessV2();

        base.OnStartup(e);

        // Set shutdown mode to close when main window closes
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        // Create and show the ruler window
        var rulerWindow = new RulerWindow();
        MainWindow = rulerWindow;
        rulerWindow.Show();

        System.Diagnostics.Debug.WriteLine("Ruler window created and shown");
    }
}


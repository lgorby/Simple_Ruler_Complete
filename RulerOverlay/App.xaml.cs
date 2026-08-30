using RulerOverlay.Helpers;
using RulerOverlay.Windows;
using System.Windows;

namespace RulerOverlay;

/// <summary>
/// Application entry point.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Must run before any window exists, otherwise the process is stuck with the
        // DPI awareness it started with and the ruler renders blurry on scaled displays.
        DpiHelper.EnablePerMonitorDpiAwarenessV2();

        base.OnStartup(e);

        // The ruler is the only top-level window, so closing it exits the app.
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var rulerWindow = new RulerWindow();
        MainWindow = rulerWindow;
        rulerWindow.Show();
    }
}

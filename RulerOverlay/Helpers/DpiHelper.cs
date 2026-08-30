using RulerOverlay.Models;
using System;
using System.Runtime.InteropServices;

namespace RulerOverlay.Helpers
{
    /// <summary>
    /// Process-wide DPI awareness setup and system PPI lookup.
    /// Per-visual DPI conversions live in <see cref="ScreenHelper"/>.
    /// </summary>
    public static class DpiHelper
    {
        // DPI awareness context handles (documented values, passed as pseudo-handles).
        private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new(-4);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int LOGPIXELSX = 88;

        /// <summary>
        /// Enables per-monitor DPI awareness V2 so the ruler stays crisp and correctly
        /// sized when moved between displays with different scaling.
        /// Must be called before any window is created.
        /// </summary>
        public static void EnablePerMonitorDpiAwarenessV2()
        {
            try
            {
                // Available from Windows 10 1703. EntryPointNotFoundException on older builds.
                if (SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2))
                    return;
            }
            catch (EntryPointNotFoundException)
            {
                // Expected on pre-1703 Windows; fall through to the legacy API.
            }
            catch (DllNotFoundException)
            {
                return;
            }

            try
            {
                SetProcessDPIAware();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DpiHelper] DPI awareness setup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// The system's reported horizontal DPI. This is the scaling DPI Windows uses for
        /// layout, not the monitor's true physical pixel density, so it is only a starting
        /// point until the user calibrates.
        /// </summary>
        public static double GetSystemDpi()
        {
            IntPtr hdc = IntPtr.Zero;

            try
            {
                hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                    return RulerDefaults.Ppi;

                var dpi = GetDeviceCaps(hdc, LOGPIXELSX);
                return dpi > 0 ? dpi : RulerDefaults.Ppi;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DpiHelper] GetSystemDpi failed: {ex.Message}");
                return RulerDefaults.Ppi;
            }
            finally
            {
                if (hdc != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, hdc);
            }
        }
    }
}

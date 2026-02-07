using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace RulerOverlay.Helpers
{
    /// <summary>
    /// Helper for DPI awareness and screen PPI calculations
    /// </summary>
    public static class DpiHelper
    {
        // DPI Awareness Context values
        private const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(int dpiFlag);

        /// <summary>
        /// Enables per-monitor DPI awareness V2 for the application
        /// Must be called before any windows are created
        /// </summary>
        public static void EnablePerMonitorDpiAwarenessV2()
        {
            try
            {
                // Try V2 first (Windows 10 1703+)
                if (!SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                {
                    // Fallback to older API
                    SetProcessDPIAware();
                }
            }
            catch
            {
                // Fallback to older API if newer one not available
                SetProcessDPIAware();
            }
        }

        /// <summary>
        /// Gets the system default PPI (typically 96 for standard DPI)
        /// </summary>
        public static double GetScreenPpi()
        {
            try
            {
                using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    return graphics.DpiX; // Get horizontal DPI
                }
            }
            catch
            {
                return 96; // Default Windows DPI
            }
        }

        /// <summary>
        /// Gets the effective PPI to use for measurements
        /// Prefers calibrated PPI, falls back to system PPI
        /// </summary>
        /// <param name="calibratedPpi">User-calibrated PPI value</param>
        /// <returns>Effective PPI to use</returns>
        public static double GetEffectivePpi(double calibratedPpi)
        {
            return calibratedPpi > 0 ? calibratedPpi : GetScreenPpi();
        }
    }
}

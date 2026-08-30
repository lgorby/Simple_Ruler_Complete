using System;
using System.Threading;
using System.Windows;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Copies formatted measurements to the Windows clipboard.
    /// Formatting itself lives in <see cref="MeasurementEngine"/>; this service only
    /// deals with the clipboard, which another process can hold open at any moment.
    /// </summary>
    public static class ClipboardService
    {
        private const int MaxAttempts = 5;
        private const int RetryDelayMs = 60;

        /// <summary>
        /// Copies text to the clipboard, retrying briefly if another process has it locked.
        /// </summary>
        /// <returns>True when the text was placed on the clipboard.</returns>
        public static bool CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(text, copy: true);
                    return true;
                }
                catch (Exception ex) when (attempt < MaxAttempts)
                {
                    // The clipboard is a shared, singly-owned system resource: another
                    // application can hold it open and make this throw transiently.
                    System.Diagnostics.Debug.WriteLine(
                        $"[ClipboardService] Attempt {attempt} failed: {ex.Message}");
                    Thread.Sleep(RetryDelayMs);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClipboardService] Copy failed: {ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}

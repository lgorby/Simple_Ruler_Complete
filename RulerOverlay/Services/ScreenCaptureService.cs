using RulerOverlay.Helpers;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Captures rectangular areas of the screen with Win32 BitBlt.
    /// Used by the magnifier and by edge detection.
    /// </summary>
    public class ScreenCaptureService
    {
        /// <summary>
        /// Captures a screen region.
        /// </summary>
        /// <param name="x">Left coordinate, in physical screen pixels.</param>
        /// <param name="y">Top coordinate, in physical screen pixels.</param>
        /// <param name="width">Width in physical pixels.</param>
        /// <param name="height">Height in physical pixels.</param>
        /// <returns>A frozen bitmap of the region, or null if the capture failed.</returns>
        public BitmapSource? CaptureScreenArea(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
                return null;

            IntPtr screenDc = IntPtr.Zero;
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;

            // Every GDI object is released in the finally block, so no handle leaks
            // even when a step fails or an exception unwinds the method.
            try
            {
                screenDc = Win32Helper.GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero)
                    return null;

                memDc = Win32Helper.CreateCompatibleDC(screenDc);
                if (memDc == IntPtr.Zero)
                    return null;

                hBitmap = Win32Helper.CreateCompatibleBitmap(screenDc, width, height);
                if (hBitmap == IntPtr.Zero)
                    return null;

                oldBitmap = Win32Helper.SelectObject(memDc, hBitmap);

                bool copied = Win32Helper.BitBlt(
                    memDc, 0, 0, width, height,
                    screenDc, x, y,
                    Win32Helper.SRCCOPY);

                if (!copied)
                    return null;

                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // Freezing makes the bitmap safe to hand to the UI and cheaper to render.
                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScreenCaptureService] Capture failed: {ex.Message}");
                return null;
            }
            finally
            {
                if (memDc != IntPtr.Zero && oldBitmap != IntPtr.Zero)
                    Win32Helper.SelectObject(memDc, oldBitmap);

                if (hBitmap != IntPtr.Zero)
                    Win32Helper.DeleteObject(hBitmap);

                if (memDc != IntPtr.Zero)
                    Win32Helper.DeleteDC(memDc);

                if (screenDc != IntPtr.Zero)
                    Win32Helper.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}

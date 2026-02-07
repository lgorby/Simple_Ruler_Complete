using RulerOverlay.Helpers;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Service for capturing screen areas using Win32 BitBlt
    /// Used by magnifier to capture and display screen pixels
    /// </summary>
    public class ScreenCaptureService
    {
        /// <summary>
        /// Captures a rectangular area of the screen
        /// </summary>
        /// <param name="x">Left coordinate</param>
        /// <param name="y">Top coordinate</param>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        /// <returns>BitmapSource of captured area</returns>
        public BitmapSource? CaptureScreenArea(int x, int y, int width, int height)
        {
            try
            {
                // Get screen DC
                IntPtr screenDc = Win32Helper.GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero)
                    return null;

                // Create compatible DC
                IntPtr memDc = Win32Helper.CreateCompatibleDC(screenDc);
                if (memDc == IntPtr.Zero)
                {
                    Win32Helper.ReleaseDC(IntPtr.Zero, screenDc);
                    return null;
                }

                // Create compatible bitmap
                IntPtr hBitmap = Win32Helper.CreateCompatibleBitmap(screenDc, width, height);
                if (hBitmap == IntPtr.Zero)
                {
                    Win32Helper.DeleteDC(memDc);
                    Win32Helper.ReleaseDC(IntPtr.Zero, screenDc);
                    return null;
                }

                // Select bitmap into DC
                IntPtr oldBitmap = Win32Helper.SelectObject(memDc, hBitmap);

                // Copy screen to bitmap
                bool success = Win32Helper.BitBlt(
                    memDc, 0, 0, width, height,
                    screenDc, x, y,
                    Win32Helper.SRCCOPY
                );

                if (!success)
                {
                    Win32Helper.SelectObject(memDc, oldBitmap);
                    Win32Helper.DeleteObject(hBitmap);
                    Win32Helper.DeleteDC(memDc);
                    Win32Helper.ReleaseDC(IntPtr.Zero, screenDc);
                    return null;
                }

                // Convert HBITMAP to BitmapSource
                BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );

                // Freeze for better performance
                bitmapSource.Freeze();

                // Cleanup
                Win32Helper.SelectObject(memDc, oldBitmap);
                Win32Helper.DeleteObject(hBitmap);
                Win32Helper.DeleteDC(memDc);
                Win32Helper.ReleaseDC(IntPtr.Zero, screenDc);

                return bitmapSource;
            }
            catch
            {
                return null;
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Media;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace RulerOverlay.Helpers
{
    /// <summary>
    /// Screen geometry and DPI conversions.
    ///
    /// WPF works in device-independent pixels (DIPs) while Win32/WinForms report
    /// physical device pixels. Mixing the two silently produces wrong measurements and
    /// misplaced windows on any display that is not at 100% scaling, so every
    /// conversion the app needs lives here rather than being re-derived at each call site.
    /// </summary>
    public static class ScreenHelper
    {
        /// <summary>
        /// DPI scale factors for the monitor a visual currently sits on.
        /// Falls back to 1.0 before the visual has been connected to a presentation source.
        /// </summary>
        public static (double X, double Y) GetDpiScale(Visual? visual)
        {
            if (visual != null)
            {
                var source = PresentationSource.FromVisual(visual);
                var transform = source?.CompositionTarget?.TransformToDevice;
                if (transform.HasValue && transform.Value.M11 > 0 && transform.Value.M22 > 0)
                    return (transform.Value.M11, transform.Value.M22);
            }

            return (1.0, 1.0);
        }

        /// <summary>Converts a physical-pixel length to DIPs for the given scale.</summary>
        public static double ToLogical(double physical, double scale) => scale > 0 ? physical / scale : physical;

        /// <summary>Converts a DIP length to physical pixels for the given scale.</summary>
        public static double ToPhysical(double logical, double scale) => logical * scale;

        /// <summary>
        /// Bounds of the monitor nearest to a physical-pixel point, in physical pixels.
        /// </summary>
        public static Drawing.Rectangle GetScreenBoundsFromPhysicalPoint(double physicalX, double physicalY)
        {
            var screen = WinForms.Screen.FromPoint(new Drawing.Point((int)physicalX, (int)physicalY));
            return screen.Bounds;
        }

        /// <summary>
        /// Working area (excludes the taskbar) of the monitor nearest to a physical-pixel point,
        /// in physical pixels.
        /// </summary>
        public static Drawing.Rectangle GetWorkingAreaFromPhysicalPoint(double physicalX, double physicalY)
        {
            var screen = WinForms.Screen.FromPoint(new Drawing.Point((int)physicalX, (int)physicalY));
            return screen.WorkingArea;
        }

        /// <summary>
        /// The full virtual desktop across every monitor, in physical pixels.
        /// Left/Top are negative when a monitor sits above or to the left of the primary one.
        /// </summary>
        public static Drawing.Rectangle GetVirtualScreenBounds() => WinForms.SystemInformation.VirtualScreen;

        /// <summary>
        /// Physical pixel resolution of the primary monitor. Used for PPI calibration,
        /// which must reason about real pixels rather than DIPs.
        /// </summary>
        public static (int Width, int Height) GetPrimaryScreenPixelSize()
        {
            var primary = WinForms.Screen.PrimaryScreen;
            if (primary != null)
                return (primary.Bounds.Width, primary.Bounds.Height);

            // Fall back to WPF's DIP-based numbers if WinForms cannot identify a primary screen.
            return ((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
        }

        /// <summary>
        /// Diagonal of the primary monitor in physical pixels, i.e. sqrt(w² + h²).
        /// </summary>
        public static double GetPrimaryScreenDiagonalPixels()
        {
            var (width, height) = GetPrimaryScreenPixelSize();
            return Math.Sqrt((double)width * width + (double)height * height);
        }

        /// <summary>
        /// True when enough of a rectangle (given in physical pixels) overlaps some monitor
        /// for the user to still grab it.
        ///
        /// Every monitor is checked, not just the primary one, so a ruler saved on a
        /// secondary display - which can legitimately sit at negative coordinates - is not
        /// treated as lost. The test is deliberately lenient: it asks for
        /// <paramref name="minOverlap"/> pixels along the long axis but only half that
        /// proportion of the height, so a ruler nudged partly past the bottom of the screen
        /// stays where the user put it instead of being yanked back to the centre.
        /// </summary>
        public static bool IsReachable(Drawing.Rectangle bounds, int minOverlap)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            int requiredWidth = Math.Min(minOverlap, bounds.Width);
            int requiredHeight = Math.Max(1, Math.Min(minOverlap, bounds.Height) / 2);

            foreach (var screen in WinForms.Screen.AllScreens)
            {
                var intersection = Drawing.Rectangle.Intersect(screen.Bounds, bounds);
                if (intersection.Width >= requiredWidth && intersection.Height >= requiredHeight)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Places a fixed-size overlay in whichever corner of the cursor's monitor is
        /// furthest from the cursor, so it never sits under what the user is pointing at.
        ///
        /// Returned in device-independent pixels, ready for a Popup's Horizontal/Vertical
        /// offsets, which are DIP-based even though the working area is reported in
        /// physical pixels.
        /// </summary>
        /// <param name="cursorPhysicalX">Cursor X in physical screen pixels.</param>
        /// <param name="cursorPhysicalY">Cursor Y in physical screen pixels.</param>
        /// <param name="sizeDip">Edge length of the overlay, in DIPs.</param>
        /// <param name="marginDip">Gap to leave against the screen edges, in DIPs.</param>
        /// <param name="dpiScale">Physical pixels per DIP on that monitor.</param>
        public static (double X, double Y) GetOverlayCornerPosition(
            double cursorPhysicalX, double cursorPhysicalY,
            double sizeDip, double marginDip, double dpiScale)
        {
            var work = GetWorkingAreaFromPhysicalPoint(cursorPhysicalX, cursorPhysicalY);

            double workLeft = ToLogical(work.Left, dpiScale);
            double workTop = ToLogical(work.Top, dpiScale);
            double workRight = ToLogical(work.Right, dpiScale);
            double workBottom = ToLogical(work.Bottom, dpiScale);

            bool cursorInRightHalf = cursorPhysicalX > work.Left + work.Width / 2.0;
            bool cursorInBottomHalf = cursorPhysicalY > work.Top + work.Height / 2.0;

            return (
                cursorInRightHalf ? workLeft + marginDip : workRight - sizeDip - marginDip,
                cursorInBottomHalf ? workTop + marginDip : workBottom - sizeDip - marginDip);
        }

        /// <summary>
        /// Centers a rectangle of the given size on the primary monitor, in physical pixels.
        /// </summary>
        public static Drawing.Point GetCenteredPosition(int width, int height)
        {
            var primary = WinForms.Screen.PrimaryScreen;
            var bounds = primary?.WorkingArea ?? new Drawing.Rectangle(
                0, 0,
                (int)SystemParameters.PrimaryScreenWidth,
                (int)SystemParameters.PrimaryScreenHeight);

            return new Drawing.Point(
                bounds.Left + (bounds.Width - width) / 2,
                bounds.Top + (bounds.Height - height) / 2);
        }
    }
}

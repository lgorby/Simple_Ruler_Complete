using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Service for detecting color boundaries on screen for edge snapping
    /// Ports logic from src/services/EdgeSnapping.ts
    /// </summary>
    public class EdgeSnappingService
    {
        private readonly ScreenCaptureService _screenCapture;
        private const int ColorThreshold = 30; // Color difference threshold for edge detection
        private const int SnapTolerance = 5; // Pixels within which to snap

        public EdgeSnappingService()
        {
            _screenCapture = new ScreenCaptureService();
        }

        /// <summary>
        /// Finds snap points near the left edge of the ruler
        /// </summary>
        public List<Point> FindLeftEdgeSnapPoints(int x, int y, int height)
        {
            return FindVerticalEdgeSnapPoints(x - 10, y, 20, height);
        }

        /// <summary>
        /// Finds snap points near the right edge of the ruler
        /// </summary>
        public List<Point> FindRightEdgeSnapPoints(int x, int y, int height)
        {
            return FindVerticalEdgeSnapPoints(x - 10, y, 20, height);
        }

        /// <summary>
        /// Finds vertical edges in a screen area
        /// </summary>
        private List<Point> FindVerticalEdgeSnapPoints(int x, int y, int width, int height)
        {
            var snapPoints = new List<Point>();

            try
            {
                // Capture screen area
                var captured = _screenCapture.CaptureScreenArea(x, y, width, height);
                if (captured == null)
                    return snapPoints;

                // Convert to byte array for pixel analysis
                var pixels = GetPixelArray(captured);
                if (pixels == null)
                    return snapPoints;

                int stride = width * 4; // BGRA format

                // Scan for vertical edges
                for (int pixelY = 0; pixelY < height; pixelY++)
                {
                    for (int pixelX = 1; pixelX < width; pixelX++)
                    {
                        int offset1 = pixelY * stride + (pixelX - 1) * 4;
                        int offset2 = pixelY * stride + pixelX * 4;

                        // Get colors of adjacent pixels
                        var color1 = Color.FromRgb(pixels[offset1 + 2], pixels[offset1 + 1], pixels[offset1]);
                        var color2 = Color.FromRgb(pixels[offset2 + 2], pixels[offset2 + 1], pixels[offset2]);

                        // Check if colors differ significantly (edge detected)
                        if (GetColorDifference(color1, color2) > ColorThreshold)
                        {
                            snapPoints.Add(new Point(x + pixelX, y + pixelY));
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors during edge detection
            }

            return snapPoints;
        }

        /// <summary>
        /// Finds the closest snap point to a target position
        /// </summary>
        public Point? FindClosestSnapPoint(List<Point> snapPoints, Point target)
        {
            if (snapPoints.Count == 0)
                return null;

            Point? closest = null;
            double minDistance = double.MaxValue;

            foreach (var point in snapPoints)
            {
                double distance = Math.Abs(point.X - target.X);
                if (distance < minDistance && distance <= SnapTolerance)
                {
                    minDistance = distance;
                    closest = point;
                }
            }

            return closest;
        }

        /// <summary>
        /// Calculates color difference between two colors
        /// </summary>
        private double GetColorDifference(Color c1, Color c2)
        {
            int rDiff = Math.Abs(c1.R - c2.R);
            int gDiff = Math.Abs(c1.G - c2.G);
            int bDiff = Math.Abs(c1.B - c2.B);

            return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
        }

        /// <summary>
        /// Converts BitmapSource to byte array for pixel analysis
        /// </summary>
        private byte[]? GetPixelArray(BitmapSource source)
        {
            try
            {
                int width = source.PixelWidth;
                int height = source.PixelHeight;
                int stride = width * 4; // BGRA format
                byte[] pixels = new byte[height * stride];

                source.CopyPixels(pixels, stride, 0);

                return pixels;
            }
            catch
            {
                return null;
            }
        }
    }
}

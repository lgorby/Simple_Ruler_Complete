using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Detects strong vertical colour boundaries on screen so a ruler edge can snap to them.
    ///
    /// Rather than returning every pixel that differs from its neighbour (which is mostly
    /// noise, e.g. antialiased text), each candidate column is scored by how many sampled
    /// rows show a colour change there. A real UI border produces a change down most of the
    /// column, while text and gradients do not.
    /// </summary>
    public class EdgeSnappingService
    {
        /// <summary>Per-channel RGB distance at which two neighbouring pixels count as an edge.</summary>
        private const double ColorThreshold = 30.0;

        /// <summary>How far either side of the requested position to look, in physical pixels.</summary>
        public const int SnapTolerance = 8;

        /// <summary>Fraction of sampled rows that must agree before a column is treated as an edge.</summary>
        private const double MinColumnAgreement = 0.6;

        /// <summary>Rows are sampled rather than read exhaustively, to keep this cheap during a drag.</summary>
        private const int MaxSampledRows = 40;

        private readonly ScreenCaptureService _screenCapture;

        public EdgeSnappingService(ScreenCaptureService? screenCapture = null)
        {
            _screenCapture = screenCapture ?? new ScreenCaptureService();
        }

        /// <summary>
        /// Finds the nearest strong vertical edge to <paramref name="physicalX"/>.
        /// All coordinates are physical screen pixels.
        /// </summary>
        /// <param name="physicalX">Screen X the ruler edge currently sits at.</param>
        /// <param name="physicalTop">Top of the band to scan.</param>
        /// <param name="physicalHeight">Height of the band to scan.</param>
        /// <returns>The screen X to snap to, or null when nothing convincing is nearby.</returns>
        public double? FindNearestVerticalEdge(double physicalX, double physicalTop, int physicalHeight)
        {
            if (physicalHeight <= 1)
                return null;

            // Scan a band centred on the requested position. One extra column on the left
            // gives every candidate column a left-hand neighbour to compare against.
            int bandWidth = SnapTolerance * 2 + 2;
            int bandLeft = (int)Math.Round(physicalX) - SnapTolerance - 1;
            int bandTop = (int)Math.Round(physicalTop);

            var captured = _screenCapture.CaptureScreenArea(bandLeft, bandTop, bandWidth, physicalHeight);
            if (captured == null)
                return null;

            var pixels = TryGetBgraPixels(captured, out int stride);
            if (pixels == null)
                return null;

            int width = captured.PixelWidth;
            int height = captured.PixelHeight;
            if (width < 2 || height < 1)
                return null;

            // Sample evenly spaced rows instead of all of them.
            int rowStep = Math.Max(1, height / MaxSampledRows);
            int sampledRows = 0;
            for (int y = 0; y < height; y += rowStep)
                sampledRows++;

            if (sampledRows == 0)
                return null;

            int requiredHits = (int)Math.Ceiling(sampledRows * MinColumnAgreement);
            double? bestX = null;
            int bestScore = 0;
            double bestDistance = double.MaxValue;

            for (int column = 1; column < width; column++)
            {
                int hits = 0;

                for (int y = 0; y < height; y += rowStep)
                {
                    int rowOffset = y * stride;
                    int left = rowOffset + (column - 1) * 4;
                    int right = rowOffset + column * 4;

                    if (GetColorDistance(pixels, left, right) > ColorThreshold)
                        hits++;
                }

                if (hits < requiredHits)
                    continue;

                double candidateX = bandLeft + column;
                double distance = Math.Abs(candidateX - physicalX);

                if (distance > SnapTolerance)
                    continue;

                // The nearest qualifying column wins, with strength only breaking ties.
                // Preferring the strongest instead lets a marginally crisper boundary a few
                // pixels further away beat the one the user is plainly aiming at, which
                // reads as the snap missing.
                if (distance < bestDistance || (distance == bestDistance && hits > bestScore))
                {
                    bestScore = hits;
                    bestDistance = distance;
                    bestX = candidateX;
                }
            }

            return bestX;
        }

        /// <summary>
        /// Euclidean RGB distance between two BGRA pixels in the same buffer.
        /// </summary>
        private static double GetColorDistance(byte[] pixels, int offsetA, int offsetB)
        {
            int bDiff = pixels[offsetA] - pixels[offsetB];
            int gDiff = pixels[offsetA + 1] - pixels[offsetB + 1];
            int rDiff = pixels[offsetA + 2] - pixels[offsetB + 2];

            return Math.Sqrt((double)rDiff * rDiff + (double)gDiff * gDiff + (double)bDiff * bDiff);
        }

        /// <summary>
        /// Copies a bitmap into a 32-bit BGRA buffer.
        /// Converts first when the source is not already 32bpp, so the 4-bytes-per-pixel
        /// arithmetic above is always valid.
        /// </summary>
        private static byte[]? TryGetBgraPixels(BitmapSource source, out int stride)
        {
            stride = 0;

            try
            {
                BitmapSource bgra = source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Bgr32
                    ? source
                    : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

                stride = bgra.PixelWidth * 4;
                var pixels = new byte[stride * bgra.PixelHeight];
                bgra.CopyPixels(pixels, stride, 0);
                return pixels;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EdgeSnappingService] Pixel read failed: {ex.Message}");
                return null;
            }
        }
    }
}

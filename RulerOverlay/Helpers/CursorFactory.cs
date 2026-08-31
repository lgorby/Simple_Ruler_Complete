using System;
using System.IO;
using System.Windows.Input;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace RulerOverlay.Helpers
{
    /// <summary>
    /// Builds the curved double-headed arrow used for the rotation zones.
    ///
    /// WPF ships no rotation cursor, and design tools universally use this glyph for the
    /// gesture, so it is drawn here and packaged as an in-memory .cur rather than shipped
    /// as a binary resource.
    /// </summary>
    public static class CursorFactory
    {
        private const int Size = 32;
        private const int Hotspot = Size / 2;

        private static Cursor? _rotationCursor;

        /// <summary>
        /// The rotation cursor, built once and reused. Falls back to the standard hand if
        /// the drawing or packaging fails, so a cursor problem can never break the gesture.
        /// </summary>
        public static Cursor Rotation
        {
            get
            {
                if (_rotationCursor != null)
                    return _rotationCursor;

                try
                {
                    using var bitmap = DrawRotationGlyph();
                    using var stream = PackageAsCursor(bitmap, Hotspot, Hotspot);
                    _rotationCursor = new Cursor(stream);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CursorFactory] Falling back: {ex.Message}");
                    _rotationCursor = Cursors.Hand;
                }

                return _rotationCursor;
            }
        }

        /// <summary>
        /// Draws an open circular arc with an arrowhead at each end. Stroked black
        /// underneath and white on top so it stays visible over any background.
        /// </summary>
        private static Drawing.Bitmap DrawRotationGlyph()
        {
            var bitmap = new Drawing.Bitmap(Size, Size, Drawing.Imaging.PixelFormat.Format32bppArgb);

            using var g = Drawing.Graphics.FromImage(bitmap);
            g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Drawing.Color.Transparent);

            const float radius = 9f;
            var box = new Drawing.RectangleF(Hotspot - radius, Hotspot - radius, radius * 2, radius * 2);

            // A gap at each end leaves room for the arrowheads.
            const float startAngle = 40f;
            const float sweepAngle = 260f;

            foreach (var (colour, width) in new[]
                     {
                         (Drawing.Color.Black, 4f),
                         (Drawing.Color.White, 2f)
                     })
            {
                using var pen = new Drawing.Pen(colour, width)
                {
                    StartCap = Drawing2D.LineCap.Round,
                    EndCap = Drawing2D.LineCap.Round
                };

                g.DrawArc(pen, box, startAngle, sweepAngle);
            }

            DrawArrowHead(g, startAngle, pointingClockwise: false, radius);
            DrawArrowHead(g, startAngle + sweepAngle, pointingClockwise: true, radius);

            return bitmap;
        }

        /// <summary>
        /// Puts a filled triangle at one end of the arc, aimed along the tangent.
        /// </summary>
        private static void DrawArrowHead(Drawing.Graphics g, float angleDegrees, bool pointingClockwise, float radius)
        {
            double angle = angleDegrees * Math.PI / 180.0;
            float cx = Hotspot + radius * (float)Math.Cos(angle);
            float cy = Hotspot + radius * (float)Math.Sin(angle);

            // Tangent direction at this point on the circle.
            float tx = -(float)Math.Sin(angle);
            float ty = (float)Math.Cos(angle);
            if (!pointingClockwise)
            {
                tx = -tx;
                ty = -ty;
            }

            // Normal, used to give the triangle its width.
            float nx = -ty;
            float ny = tx;

            const float length = 6f;
            const float halfWidth = 4f;

            var points = new[]
            {
                new Drawing.PointF(cx + tx * length, cy + ty * length),
                new Drawing.PointF(cx + nx * halfWidth, cy + ny * halfWidth),
                new Drawing.PointF(cx - nx * halfWidth, cy - ny * halfWidth)
            };

            using var outline = new Drawing.Pen(Drawing.Color.Black, 2f);
            g.FillPolygon(Drawing.Brushes.White, points);
            g.DrawPolygon(outline, points);
            g.FillPolygon(Drawing.Brushes.White, points);
        }

        /// <summary>
        /// Wraps a bitmap in the .cur container WPF's Cursor constructor expects: an icon
        /// directory whose entry carries the hotspot, then a DIB holding the colour bitmap
        /// and a (fully opaque) AND mask.
        /// </summary>
        private static MemoryStream PackageAsCursor(Drawing.Bitmap bitmap, int hotspotX, int hotspotY)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;

            int xorStride = width * 4;
            int xorSize = xorStride * height;

            // The AND mask is 1bpp with rows padded to 4 bytes.
            int andStride = ((width + 31) / 32) * 4;
            int andSize = andStride * height;

            const int headerSize = 40;
            int imageSize = headerSize + xorSize + andSize;

            var stream = new MemoryStream();
            var w = new BinaryWriter(stream);

            // ICONDIR
            w.Write((ushort)0);
            w.Write((ushort)2);   // 2 = cursor
            w.Write((ushort)1);

            // ICONDIRENTRY
            w.Write((byte)width);
            w.Write((byte)height);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)hotspotX);
            w.Write((ushort)hotspotY);
            w.Write(imageSize);
            w.Write(6 + 16);      // offset past ICONDIR + this entry

            // BITMAPINFOHEADER - height is doubled to cover both masks
            w.Write(headerSize);
            w.Write(width);
            w.Write(height * 2);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(0);
            w.Write(xorSize + andSize);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);

            // Colour bitmap, bottom-up as DIBs require
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    w.Write(pixel.B);
                    w.Write(pixel.G);
                    w.Write(pixel.R);
                    w.Write(pixel.A);
                }
            }

            // AND mask: zeroed, because the alpha channel already carries transparency.
            w.Write(new byte[andSize]);

            w.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}

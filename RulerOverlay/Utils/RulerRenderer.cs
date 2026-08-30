using RulerOverlay.Models;
using RulerOverlay.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace RulerOverlay.Utils
{
    /// <summary>
    /// Draws ruler tick marks and labels onto a canvas.
    ///
    /// All three units share one drawing pass. A unit only supplies its scale
    /// (<see cref="TickScale"/>): how many pixels lie between the smallest ticks, how
    /// often a longer tick occurs, and how a labelled tick is captioned. Adding a unit
    /// therefore means adding a scale, not another near-duplicate draw method.
    /// </summary>
    public static class RulerRenderer
    {
        // Tick lengths as a fraction of ruler height, indexed by tick rank
        // (0 = smallest subdivision, higher = more significant).
        private static readonly double[] TickHeightRatios = { 0.08, 0.16, 0.28, 0.40 };

        private const double LabelFontSize = 10;
        private const double LabelOffsetX = -10;
        private const double TotalLabelFontSize = 11;

        /// <summary>
        /// Width of the contrasting outline drawn under the tick marks.
        ///
        /// Opacity applies only to the ruler's background, so at low opacity the markings sit
        /// on the desktop rather than on the ruler colour they were picked to contrast with.
        /// The outline is what keeps them readable whatever shows through. Kept narrow so
        /// adjacent millimetre ticks do not merge into a band.
        /// </summary>
        private const double TickHaloThickness = 2.5;

        /// <summary>Width of the outline drawn around label glyphs.</summary>
        private const double TextHaloThickness = 2.5;

        /// <summary>Width reserved at the right end for the overall-length caption.</summary>
        private const double TotalLabelWidth = 55;

        /// <summary>Extra clearance so a tick label never collides with that caption.</summary>
        private const double TotalLabelClearance = 10;

        /// <summary>
        /// Glyph geometry is built in the canvas's own units, which are already real screen
        /// pixels, so no further DPI scaling is applied here.
        /// </summary>
        private const double LabelDpi = 1.0;

        private static readonly MeasurementEngine TotalLabelEngine = new();

        // Markings must contrast with whatever the ruler is painted, so the brushes come
        // from the current colour instead of being fixed. Rendering is single-threaded, and
        // the colour changes rarely, so caching the last pair is enough.
        private static string? _cachedColorName;
        private static Brush _inkBrush = RulerColors.CreateInkBrush(RulerDefaults.Color);
        private static Brush _accentBrush = RulerColors.CreateAccentBrush(RulerDefaults.Color);
        private static Brush _haloBrush = Frozen(RulerColors.ResolveHalo(RulerDefaults.Color));

        private static void UseColor(string? colorName)
        {
            if (string.Equals(_cachedColorName, colorName, StringComparison.OrdinalIgnoreCase))
                return;

            _cachedColorName = colorName;
            _inkBrush = RulerColors.CreateInkBrush(colorName);
            _accentBrush = RulerColors.CreateAccentBrush(colorName);
            _haloBrush = Frozen(RulerColors.ResolveHalo(colorName));
        }

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Describes the tick spacing for one unit.
        /// </summary>
        /// <param name="StepPixels">Pixel distance between the smallest ticks.</param>
        /// <param name="Ranks">
        /// Multiples of <paramref name="StepPixels"/> at which each successively longer tick
        /// appears, ordered smallest-first. A tick's rank is the number of entries it divides into.
        /// </param>
        /// <param name="LabelEvery">Ticks between labels, as a multiple of <paramref name="StepPixels"/>.</param>
        /// <param name="FormatLabel">Renders the caption for a labelled tick, given its distance in pixels.</param>
        private sealed record TickScale(
            double StepPixels,
            int[] Ranks,
            int LabelEvery,
            Func<double, string> FormatLabel);

        /// <summary>
        /// Redraws the markings for the given ruler geometry.
        /// </summary>
        /// <param name="canvas">Target canvas; cleared before drawing.</param>
        /// <param name="width">Ruler length in pixels.</param>
        /// <param name="height">Ruler thickness in pixels.</param>
        /// <param name="unit">Unit to render.</param>
        /// <param name="ppi">Calibrated pixels per inch, used for the physical units.</param>
        /// <param name="rotation">Ruler rotation, so labels can counter-rotate and stay upright.</param>
        /// <param name="pixelScale">
        /// Physical pixels per DIP on the current monitor. The canvas is drawn in ruler
        /// pixels and scaled down by this factor for display, so text is sized up by it to
        /// keep a constant apparent size on screen.
        /// </param>
        /// <param name="colorName">
        /// The ruler's background colour, so tick marks and labels can be drawn in a shade
        /// that actually contrasts with it.
        /// </param>
        public static void DrawMarkings(Canvas canvas, double width, double height,
                                        MeasurementUnit unit, int ppi, int rotation = 0,
                                        double pixelScale = 1.0, string? colorName = null)
        {
            if (canvas == null)
                return;

            UseColor(colorName ?? RulerDefaults.Color);

            canvas.Children.Clear();

            if (width <= 0 || height <= 0)
                return;

            var scale = GetScale(unit, ppi);
            if (scale.StepPixels <= 0)
                return;

            if (pixelScale <= 0)
                pixelScale = 1.0;

            DrawTicks(canvas, width, height, scale, rotation, pixelScale);
            DrawTotalLabel(canvas, width, height, unit, ppi, rotation, pixelScale);
        }

        /// <summary>
        /// Builds the tick scale for a unit.
        /// </summary>
        private static TickScale GetScale(MeasurementUnit unit, int ppi)
        {
            double safePpi = ppi > 0 ? ppi : RulerDefaults.Ppi;

            return unit switch
            {
                // Eighth-inch subdivisions; longer ticks at 1/4, 1/2 and whole inches.
                MeasurementUnit.Inches => new TickScale(
                    StepPixels: safePpi / 8.0,
                    Ranks: new[] { 2, 4, 8 },
                    LabelEvery: 8,
                    FormatLabel: pixels => FormatNumber(pixels / safePpi, 0) + "\""),

                // Millimetre subdivisions; longer ticks at 5 mm and whole centimetres.
                MeasurementUnit.Centimeters => new TickScale(
                    StepPixels: safePpi / MeasurementUnits.CentimetersPerInch / 10.0,
                    Ranks: new[] { 5, 10 },
                    LabelEvery: 10,
                    FormatLabel: pixels =>
                        FormatNumber(pixels / (safePpi / MeasurementUnits.CentimetersPerInch), 0) + "cm"),

                // 5 px subdivisions; longer ticks at 10, 50 and 100 px.
                _ => new TickScale(
                    StepPixels: 5,
                    Ranks: new[] { 2, 10, 20 },
                    LabelEvery: 10,
                    FormatLabel: pixels => FormatNumber(pixels, 0))
            };
        }

        /// <summary>
        /// Draws every tick and its label.
        ///
        /// All ticks go into a single geometry rendered by one Path, rather than one Line
        /// element per tick. A centimetre ruler has a tick every millimetre, so the
        /// element-per-tick approach built several hundred framework objects on every
        /// redraw - and a redraw happens on every mouse move during a resize drag.
        ///
        /// The loop counts whole steps and multiplies, rather than accumulating
        /// <c>x += step</c>. With a fractional step (any calibrated PPI) accumulation
        /// drifts, which previously made "is this a whole inch?" tests miss on long rulers.
        /// </summary>
        private static void DrawTicks(Canvas canvas, double width, double height, TickScale scale,
                                      int rotation, double pixelScale)
        {
            int stepCount = (int)Math.Floor(width / scale.StepPixels);

            // The overall-length caption occupies the right end; labels there are suppressed.
            double labelCutoff = width - (TotalLabelWidth + TotalLabelClearance) * pixelScale;

            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                for (int step = 0; step <= stepCount; step++)
                {
                    double x = step * scale.StepPixels;
                    if (x > width)
                        break;

                    double tickHeight = height * GetTickHeightRatio(step, scale.Ranks);

                    // A 1px stroke straddles its coordinate, so a tick drawn exactly on 0 or
                    // on the far edge has half its width clipped away and the body appears to
                    // overhang the scale. Nudging the two boundary ticks inward by half a
                    // pixel makes them fall wholly inside the first and last pixel columns,
                    // which is where positions 0 and width actually live.
                    double tickX = Math.Clamp(x, 0.5, width - 0.5);

                    // Ticks are mirrored on the top and bottom edges, so the ruler reads
                    // correctly whichever way it is rotated.
                    ctx.BeginFigure(new Point(tickX, 0), isFilled: false, isClosed: false);
                    ctx.LineTo(new Point(tickX, tickHeight), isStroked: true, isSmoothJoin: false);

                    ctx.BeginFigure(new Point(tickX, height - tickHeight), isFilled: false, isClosed: false);
                    ctx.LineTo(new Point(tickX, height), isStroked: true, isSmoothJoin: false);

                    bool isLabelled = step > 0 && step % scale.LabelEvery == 0;
                    if (isLabelled && x < labelCutoff)
                        DrawTopLabel(canvas, scale.FormatLabel(x), x, tickHeight, rotation, pixelScale);
                }
            }

            geometry.Freeze();

            // Outline first, ink over it. A wider stroke of the opposite tone underneath
            // gives a crisp edge on any backdrop; a blurred glow smears 1px ticks instead.
            canvas.Children.Add(new Path
            {
                Data = geometry,
                Stroke = _haloBrush,
                StrokeThickness = TickHaloThickness,
                SnapsToDevicePixels = true
            });

            canvas.Children.Add(new Path
            {
                Data = geometry,
                Stroke = _inkBrush,
                StrokeThickness = 1,
                SnapsToDevicePixels = true
            });
        }

        /// <summary>
        /// Tick length as a fraction of ruler height.
        ///
        /// A tick's significance is how many of the scale's thresholds its step index divides
        /// evenly into (step 0 is always the most significant). Ranks are then anchored to the
        /// top of <see cref="TickHeightRatios"/>, so the most significant tick of any unit —
        /// a whole inch, a whole centimetre, 100 px — is always drawn full length, whether
        /// that unit defines two subdivision levels or three.
        /// </summary>
        private static double GetTickHeightRatio(int step, int[] ranks)
        {
            int rank;

            if (step == 0)
            {
                rank = ranks.Length;
            }
            else
            {
                rank = 0;
                foreach (var threshold in ranks)
                {
                    if (threshold > 0 && step % threshold == 0)
                        rank++;
                }
            }

            int index = TickHeightRatios.Length - 1 - (ranks.Length - rank);
            index = Math.Clamp(index, 0, TickHeightRatios.Length - 1);

            return TickHeightRatios[index];
        }

        /// <summary>
        /// Draws a tick's caption just below the top tick, counter-rotated so it stays upright.
        /// </summary>
        private static void DrawTopLabel(Canvas canvas, string text, double x, double topTickHeight,
                                         int rotation, double pixelScale)
        {
            var label = CreateOutlinedText(text, LabelFontSize * pixelScale, _inkBrush, FontWeights.Normal);
            ApplyCounterRotation(label, rotation);

            Canvas.SetLeft(label, x + LabelOffsetX * pixelScale);
            Canvas.SetTop(label, topTickHeight + 2);
            canvas.Children.Add(label);
        }

        /// <summary>
        /// Draws the ruler's overall length, centered vertically at the right end.
        /// </summary>
        private static void DrawTotalLabel(Canvas canvas, double width, double height,
                                           MeasurementUnit unit, int ppi, int rotation,
                                           double pixelScale)
        {
            // Rendering is always on the UI thread, so one shared engine is safe here and
            // avoids an allocation on every redraw.
            TotalLabelEngine.Ppi = ppi;

            var label = CreateOutlinedText(
                TotalLabelEngine.Format(width, unit),
                TotalLabelFontSize * pixelScale,
                _accentBrush,
                FontWeights.Bold);

            ApplyCounterRotation(label, rotation);

            Canvas.SetLeft(label, width - TotalLabelWidth * pixelScale);
            Canvas.SetTop(label, height / 2 - 8 * pixelScale);
            canvas.Children.Add(label);
        }

        /// <summary>
        /// Builds a text label as filled glyph geometry with a contrasting outline around it.
        ///
        /// A plain TextBlock is legible only against the backdrop its colour was chosen for.
        /// Because opacity applies to the ruler's background alone, at low opacity the label
        /// is effectively sitting on the desktop, and near-black digits over a dark window
        /// simply vanish. Outlining the glyphs guarantees an edge whatever is behind them,
        /// and unlike a blurred glow it stays sharp at these small sizes.
        /// </summary>
        private static Canvas CreateOutlinedText(string text, double fontSize, Brush fill, FontWeight weight)
        {
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
                fontSize,
                fill,
                LabelDpi);

            var geometry = formatted.BuildGeometry(new Point(0, 0));
            geometry.Freeze();

            // Outline underneath, glyph fill on top, so the stroke never eats into the
            // letterforms the way a single stroked-and-filled path would.
            var host = new Canvas();

            host.Children.Add(new Path
            {
                Data = geometry,
                Stroke = _haloBrush,
                StrokeThickness = TextHaloThickness,
                StrokeLineJoin = PenLineJoin.Round
            });

            host.Children.Add(new Path
            {
                Data = geometry,
                Fill = fill
            });

            return host;
        }

        /// <summary>
        /// Rotates a label against the ruler's own rotation so text always reads horizontally.
        /// </summary>
        private static void ApplyCounterRotation(FrameworkElement element, int rotation)
        {
            element.RenderTransformOrigin = new Point(0.5, 0.5);

            if (RulerDefaults.NormalizeRotation(rotation) == 0)
                return;

            var transform = new RotateTransform(-rotation);
            transform.Freeze();
            element.RenderTransform = transform;
        }

        private static string FormatNumber(double value, int decimals) =>
            value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);

    }
}

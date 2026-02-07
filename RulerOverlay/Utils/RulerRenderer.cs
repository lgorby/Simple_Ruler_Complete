using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RulerOverlay.Utils
{
    /// <summary>
    /// Utility class for rendering ruler markings on a canvas
    /// </summary>
    public static class RulerRenderer
    {
        /// <summary>
        /// Draws ruler markings on a canvas based on unit type
        /// </summary>
        /// <param name="rotation">Current rotation angle in degrees (for counter-rotating labels)</param>
        public static void DrawMarkings(Canvas canvas, double width, double height, string unit, int ppi, int rotation = 0)
        {
            // Clear existing markings
            canvas.Children.Clear();

            switch (unit.ToLower())
            {
                case "inches":
                    DrawInchMarkings(canvas, width, height, ppi, rotation);
                    break;
                case "centimeters":
                    DrawCentimeterMarkings(canvas, width, height, ppi, rotation);
                    break;
                default: // pixels
                    DrawPixelMarkings(canvas, width, height, rotation);
                    break;
            }
        }

        /// <summary>
        /// Draws a tick mark on both sides (top and bottom) of the ruler
        /// </summary>
        private static void DrawDualTick(Canvas canvas, double x, double tickHeight, double rulerHeight)
        {
            // Bottom tick (grows upward from bottom edge)
            var bottomLine = new Line
            {
                X1 = x,
                Y1 = rulerHeight - tickHeight,
                X2 = x,
                Y2 = rulerHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvas.Children.Add(bottomLine);

            // Top tick (grows downward from top edge)
            var topLine = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = tickHeight,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            canvas.Children.Add(topLine);
        }

        /// <summary>
        /// Draws a label on the top side of the ruler, next to the top tick marks
        /// </summary>
        private static void DrawTopLabel(Canvas canvas, string text, double x, double topTickHeight, int rotation, double offsetX = -10, double fontSize = 10)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                Foreground = Brushes.Black,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            if (rotation != 0)
            {
                label.RenderTransform = new RotateTransform(-rotation);
            }

            Canvas.SetLeft(label, x + offsetX);
            Canvas.SetTop(label, topTickHeight + 2);
            canvas.Children.Add(label);
        }

        private static void DrawPixelMarkings(Canvas canvas, double width, double height, int rotation)
        {
            // Total length label will be placed at width - 55; suppress marking labels that would overlap
            double totalLabelLeft = width - 55;

            for (double x = 0; x < width; x += 5)
            {
                double tickHeight;
                bool showLabel = false;

                if (x % 100 == 0)
                {
                    tickHeight = height * 0.4;
                    showLabel = true;
                }
                else if (x % 50 == 0)
                {
                    tickHeight = height * 0.28;
                    showLabel = true;
                }
                else if (x % 10 == 0)
                {
                    tickHeight = height * 0.16;
                }
                else
                {
                    tickHeight = height * 0.08;
                }

                DrawDualTick(canvas, x, tickHeight, height);

                if (showLabel && x > 0)
                {
                    // Skip labels that would overlap with the total length label
                    bool tooCloseToTotal = x >= totalLabelLeft - 10 && x <= width;
                    if (!tooCloseToTotal)
                    {
                        DrawTopLabel(canvas, x.ToString("F0"), x, tickHeight, rotation);
                    }
                }
            }

            // Total length label centered vertically
            var totalLabel = new TextBlock
            {
                Text = $"{width:F0} px",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkSlateGray,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            if (rotation != 0)
            {
                totalLabel.RenderTransform = new RotateTransform(-rotation);
            }

            Canvas.SetLeft(totalLabel, width - 55);
            Canvas.SetTop(totalLabel, height / 2 - 8);
            canvas.Children.Add(totalLabel);
        }

        private static void DrawInchMarkings(Canvas canvas, double width, double height, int ppi, int rotation)
        {
            double pixelsPerInch = ppi;
            double eighthInch = pixelsPerInch / 8.0;

            for (double x = 0; x < width; x += eighthInch)
            {
                double inches = x / pixelsPerInch;
                double fraction = inches % 1.0;

                double tickHeight;
                bool showLabel = false;

                if (Math.Abs(fraction) < 0.01)
                {
                    tickHeight = height * 0.4;
                    showLabel = true;
                }
                else if (Math.Abs(fraction - 0.5) < 0.01)
                {
                    tickHeight = height * 0.28;
                }
                else if (Math.Abs(fraction - 0.25) < 0.01 || Math.Abs(fraction - 0.75) < 0.01)
                {
                    tickHeight = height * 0.2;
                }
                else
                {
                    tickHeight = height * 0.12;
                }

                DrawDualTick(canvas, x, tickHeight, height);

                if (showLabel && inches >= 1)
                {
                    // Skip labels that would overlap with the total length label
                    double inchTotalLabelLeft = width - 55;
                    bool tooCloseToTotal = x >= inchTotalLabelLeft - 10 && x <= width;
                    if (!tooCloseToTotal)
                    {
                        DrawTopLabel(canvas, inches.ToString("F0") + "\"", x, tickHeight, rotation);
                    }
                }
            }

            // Total length label centered vertically
            double totalInches = width / ppi;
            var inchTotalLabel = new TextBlock
            {
                Text = $"{totalInches:F2} in",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkSlateGray,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            if (rotation != 0)
            {
                inchTotalLabel.RenderTransform = new RotateTransform(-rotation);
            }

            Canvas.SetLeft(inchTotalLabel, width - 55);
            Canvas.SetTop(inchTotalLabel, height / 2 - 8);
            canvas.Children.Add(inchTotalLabel);
        }

        private static void DrawCentimeterMarkings(Canvas canvas, double width, double height, int ppi, int rotation)
        {
            double pixelsPerCm = ppi / 2.54;
            double halfCm = pixelsPerCm / 2.0;

            for (double x = 0; x < width; x += halfCm)
            {
                double cm = x / pixelsPerCm;
                bool isWholeCm = Math.Abs(cm % 1.0) < 0.01;

                double tickHeight;
                bool showLabel = false;

                if (isWholeCm)
                {
                    tickHeight = height * 0.4;
                    showLabel = true;
                }
                else
                {
                    tickHeight = height * 0.2;
                }

                DrawDualTick(canvas, x, tickHeight, height);

                if (showLabel && cm >= 1)
                {
                    // Skip labels that would overlap with the total length label
                    double cmTotalLabelLeft = width - 55;
                    bool tooCloseToTotal = x >= cmTotalLabelLeft - 10 && x <= width;
                    if (!tooCloseToTotal)
                    {
                        DrawTopLabel(canvas, cm.ToString("F0") + "cm", x, tickHeight, rotation, -12);
                    }
                }
            }

            // Total length label centered vertically
            double totalCm = width / (ppi / 2.54);
            var cmTotalLabel = new TextBlock
            {
                Text = $"{totalCm:F2} cm",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkSlateGray,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            if (rotation != 0)
            {
                cmTotalLabel.RenderTransform = new RotateTransform(-rotation);
            }

            Canvas.SetLeft(cmTotalLabel, width - 55);
            Canvas.SetTop(cmTotalLabel, height / 2 - 8);
            canvas.Children.Add(cmTotalLabel);
        }
    }
}

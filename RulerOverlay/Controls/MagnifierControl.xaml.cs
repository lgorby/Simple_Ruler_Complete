using RulerOverlay.Helpers;
using RulerOverlay.Models;
using RulerOverlay.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RulerOverlay.Controls
{
    /// <summary>
    /// Shows a zoomed view of the screen pixels under the cursor.
    /// </summary>
    public partial class MagnifierControl : UserControl
    {
        /// <summary>Smallest clear zone around the marked pixel, in DIPs.</summary>
        private const double MinReticleGap = 12;

        /// <summary>How often the magnified image refreshes while visible.</summary>
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(50);

        private readonly ScreenCaptureService _screenCapture = new();

        /// <summary>Reused across ticks; recreating it per mouse move churned allocations.</summary>
        private readonly MeasurementEngine _measurementEngine = new();

        private readonly DispatcherTimer _updateTimer;

        private int _zoomLevel = RulerDefaults.MagnifierZoom;

        /// <summary>Physical screen pixels per DIP, so strokes can be exactly one pixel.</summary>
        private double _pixelScale = 1.0;

        public MagnifierControl()
        {
            InitializeComponent();

            _updateTimer = new DispatcherTimer { Interval = RefreshInterval };
            _updateTimer.Tick += UpdateTimer_Tick;

            UpdateZoomCaption();
            ApplyReticleMetrics();
        }

        /// <summary>
        /// Physical screen pixels per DIP on the monitor showing the magnifier.
        /// Set by the hosting window so the reticle can be drawn one device pixel thick.
        /// </summary>
        public double PixelScale
        {
            get => _pixelScale;
            set
            {
                if (value <= 0 || Math.Abs(value - _pixelScale) < 0.0001)
                    return;

                _pixelScale = value;
                ApplyReticleMetrics();
            }
        }

        /// <summary>
        /// Sizes the reticle in real pixels.
        ///
        /// Strokes are 1/DPI device-independent pixels, which is exactly one device pixel;
        /// the marker is one magnified source pixel across; and the whole reticle is shifted
        /// half a magnified pixel because the capture puts the cursor's own pixel at the
        /// image midpoint, i.e. the midpoint is that pixel's leading edge rather than its
        /// centre.
        /// </summary>
        private void ApplyReticleMetrics()
        {
            double hairline = 1.0 / _pixelScale;
            double sourcePixel = _zoomLevel;

            // Leave clear space around the marked pixel so the arms never cover it.
            double gap = Math.Max(MinReticleGap, sourcePixel * 3);

            ReticleGapColumn.Width = new GridLength(gap);
            ReticleGapRow.Height = new GridLength(gap);

            CrosshairTop.Width = hairline;
            CrosshairBottom.Width = hairline;
            CrosshairLeft.Height = hairline;
            CrosshairRight.Height = hairline;

            PixelMarker.Width = sourcePixel;
            PixelMarker.Height = sourcePixel;
            PixelMarker.StrokeThickness = hairline;

            ReticleOffset.X = sourcePixel / 2.0;
            ReticleOffset.Y = sourcePixel / 2.0;
        }

        /// <summary>
        /// Magnification factor, clamped to the supported range.
        /// </summary>
        public int ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                var clamped = RulerDefaults.Clamp(value,
                    RulerDefaults.MinMagnifierZoom, RulerDefaults.MaxMagnifierZoom);

                if (clamped == _zoomLevel)
                    return;

                _zoomLevel = clamped;
                UpdateZoomCaption();
                ApplyReticleMetrics();
            }
        }

        /// <summary>
        /// Updates the caption showing where along the ruler the cursor sits.
        ///
        /// The ruler's total length is shown alongside it, because the cursor is normally a
        /// few pixels inside the edge and a lone position reading looks like it disagrees
        /// with the length printed on the ruler.
        /// </summary>
        /// <param name="pixelPosition">Distance from the ruler's origin, in pixels.</param>
        /// <param name="totalPixels">The ruler's full length, in pixels.</param>
        /// <param name="unit">Unit to display it in.</param>
        /// <param name="ppi">Calibrated pixels per inch.</param>
        public void UpdatePosition(double pixelPosition, double totalPixels, MeasurementUnit unit, double ppi)
        {
            _measurementEngine.Ppi = ppi;
            SetCaption(_measurementEngine.FormatWithTotal(pixelPosition, totalPixels, unit));
        }

        /// <summary>
        /// Sets the caption directly. Point-to-point mode uses this to show the live
        /// distance, which has no ruler position to report.
        /// Passing null or empty hides the caption.
        /// </summary>
        public void SetCaption(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                PositionText.Visibility = Visibility.Collapsed;
                return;
            }

            PositionText.Text = text;
            PositionText.Visibility = Visibility.Visible;
        }

        /// <summary>Begins refreshing the magnified image.</summary>
        public void Start() => _updateTimer.Start();

        /// <summary>Stops refreshing and releases the last captured frame.</summary>
        public void Stop()
        {
            _updateTimer.Stop();
            MagnifierImage.Source = null;
            PositionText.Visibility = Visibility.Collapsed;
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // A timer tick runs outside any caller's try block, so an escaping exception
            // would take the whole app down. A dropped frame is the right failure here.
            try
            {
                // GetCursorPosition reports physical pixels, which is exactly what
                // CaptureScreenArea expects - no DPI conversion belongs here.
                var cursor = Win32Helper.GetCursorPosition();

                int captureSize = Math.Max(1, (int)(RulerDefaults.MagnifierSize / _zoomLevel));
                int captureX = cursor.X - captureSize / 2;
                int captureY = cursor.Y - captureSize / 2;

                var captured = _screenCapture.CaptureScreenArea(captureX, captureY, captureSize, captureSize);
                if (captured == null)
                    return;

                var scale = new ScaleTransform(_zoomLevel, _zoomLevel);
                scale.Freeze();

                var scaled = new TransformedBitmap(captured, scale);
                scaled.Freeze();

                MagnifierImage.Source = scaled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MagnifierControl] Frame skipped: {ex.Message}");
            }
        }

        private void UpdateZoomCaption() =>
            ZoomLevelText.Text = _zoomLevel.ToString(CultureInfo.CurrentCulture) + "x";
    }
}

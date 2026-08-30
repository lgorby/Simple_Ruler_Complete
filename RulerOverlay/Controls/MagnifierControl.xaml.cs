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
        /// <summary>How often the magnified image refreshes while visible.</summary>
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(50);

        private readonly ScreenCaptureService _screenCapture = new();

        /// <summary>Reused across ticks; recreating it per mouse move churned allocations.</summary>
        private readonly MeasurementEngine _measurementEngine = new();

        private readonly DispatcherTimer _updateTimer;

        private int _zoomLevel = RulerDefaults.MagnifierZoom;

        public MagnifierControl()
        {
            InitializeComponent();

            _updateTimer = new DispatcherTimer { Interval = RefreshInterval };
            _updateTimer.Tick += UpdateTimer_Tick;

            UpdateZoomCaption();
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

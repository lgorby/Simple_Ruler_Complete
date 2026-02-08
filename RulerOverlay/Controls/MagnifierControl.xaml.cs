using RulerOverlay.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace RulerOverlay.Controls
{
    public partial class MagnifierControl : System.Windows.Controls.UserControl
    {
        private readonly ScreenCaptureService _screenCapture;
        private readonly DispatcherTimer _updateTimer;
        private int _zoomLevel = 4;
        private int _rotation = 0;

        public MagnifierControl()
        {
            InitializeComponent();

            _screenCapture = new ScreenCaptureService();

            // Update magnifier every 50ms
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }

        public int ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel = Math.Clamp(value, 2, 16);
                ZoomLevelText.Text = $"{_zoomLevel}x";
            }
        }

        public int Rotation
        {
            get => _rotation;
            set => _rotation = value;
        }

        public void UpdatePosition(double pixelPosition, string unit, double ppi)
        {
            var engine = new MeasurementEngine(ppi);
            var result = engine.Convert(pixelPosition, unit);
            PositionText.Text = result.Formatted;
            PositionText.Visibility = Visibility.Visible;
        }

        public void Start()
        {
            _updateTimer.Start();
        }

        public void Stop()
        {
            _updateTimer.Stop();
            MagnifierImage.Source = null;
            PositionText.Visibility = Visibility.Collapsed;
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Get mouse position
                var mousePosition = GetMousePosition();

                // Calculate capture area (centered on mouse)
                int captureSize = 200 / _zoomLevel;
                int captureX = (int)(mousePosition.X - captureSize / 2);
                int captureY = (int)(mousePosition.Y - captureSize / 2);

                // Capture screen area
                var captured = _screenCapture.CaptureScreenArea(
                    captureX,
                    captureY,
                    captureSize,
                    captureSize
                );

                if (captured != null)
                {
                    // Scale up the captured image
                    var scaled = new TransformedBitmap(
                        captured,
                        new ScaleTransform(_zoomLevel, _zoomLevel)
                    );

                    MagnifierImage.Source = scaled;
                }
            }
            catch
            {
                // Ignore errors during capture
            }
        }

        private System.Windows.Point GetMousePosition()
        {
            // Get cursor position using Win32
            var point = new System.Drawing.Point();
            if (GetCursorPos(ref point))
            {
                return new System.Windows.Point(point.X, point.Y);
            }
            return new System.Windows.Point(0, 0);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref System.Drawing.Point lpPoint);
    }
}

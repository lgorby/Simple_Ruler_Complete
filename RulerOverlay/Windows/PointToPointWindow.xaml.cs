using RulerOverlay.Helpers;
using RulerOverlay.Models;
using RulerOverlay.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Mouse = System.Windows.Input.Mouse;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace RulerOverlay.Windows
{
    /// <summary>
    /// Full-screen overlay for measuring the distance between two arbitrary points.
    /// </summary>
    public partial class PointToPointWindow : Window
    {
        /// <summary>Radius of the open circle around each endpoint.</summary>
        private const double MarkerRadius = 7;

        /// <summary>
        /// Clear space left at the exact endpoint. Nothing is drawn inside this radius, so
        /// the pixel actually being measured stays visible - both directly and in the
        /// magnifier, which captures the overlay along with the screen beneath it.
        /// </summary>
        private const double MarkerCentreGap = 2.5;

        /// <summary>How far the crosshair ticks reach out from the endpoint.</summary>
        private const double MarkerTickLength = 12;

        private const double MarkerStrokeThickness = 1.5;
        private const double LabelOffsetX = -40;
        private const double LabelOffsetY = -20;

        private readonly PointToPointViewModel _viewModel;

        private Line? _measurementLine;
        private Path? _endMarker;
        private TextBlock? _distanceLabel;

        /// <summary>
        /// Scale between WPF device-independent pixels and physical screen pixels.
        /// Captured once the window has a presentation source.
        /// </summary>
        private (double X, double Y) _dpiScale = (1.0, 1.0);

        public PointToPointWindow(PointToPointViewModel viewModel, int magnifierZoom = RulerDefaults.MagnifierZoom)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Magnifier.ZoomLevel = magnifierZoom;

            Loaded += PointToPointWindow_Loaded;
            Closed += PointToPointWindow_Closed;
        }

        private void PointToPointWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _dpiScale = ScreenHelper.GetDpiScale(this);

            CoverVirtualDesktop();

            // The overlay must have focus for Esc to close it.
            Activate();
            Focus();

            // Precise endpoint placement is the whole point of this mode, so the
            // magnifier starts on rather than waiting to be asked for.
            ShowMagnifier();
        }

        private void PointToPointWindow_Closed(object? sender, EventArgs e) => HideMagnifier();

        /// <summary>
        /// Stretches the overlay across every monitor.
        ///
        /// WindowState="Maximized" only covers the monitor the window opened on, which
        /// silently made it impossible to measure across a multi-monitor desktop.
        /// </summary>
        private void CoverVirtualDesktop()
        {
            var virtualScreen = ScreenHelper.GetVirtualScreenBounds();
            if (virtualScreen.Width <= 0 || virtualScreen.Height <= 0)
                return;

            WindowState = WindowState.Normal;
            Left = ScreenHelper.ToLogical(virtualScreen.Left, _dpiScale.X);
            Top = ScreenHelper.ToLogical(virtualScreen.Top, _dpiScale.Y);
            Width = ScreenHelper.ToLogical(virtualScreen.Width, _dpiScale.X);
            Height = ScreenHelper.ToLogical(virtualScreen.Height, _dpiScale.Y);
        }

        /// <summary>
        /// Converts a WPF point to physical screen pixels, so a distance labelled "px"
        /// really is a screen pixel count on a scaled display.
        /// </summary>
        private Point ToPhysical(Point logical) => new(
            ScreenHelper.ToPhysical(logical.X, _dpiScale.X),
            ScreenHelper.ToPhysical(logical.Y, _dpiScale.Y));

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Right-click leaves the mode, matching the ruler's own right-click affordance.
            if (e.ChangedButton == MouseButton.Right)
            {
                Close();
                return;
            }

            if (e.ChangedButton != MouseButton.Left)
                return;

            var position = e.GetPosition(MeasurementCanvas);
            _viewModel.StartMeasurement(ToPhysical(position));

            ClearDrawing();

            _measurementLine = new Line
            {
                Stroke = Brushes.Cyan,
                StrokeThickness = 2,
                X1 = position.X,
                Y1 = position.Y,
                X2 = position.X,
                Y2 = position.Y
            };
            MeasurementCanvas.Children.Add(_measurementLine);

            MeasurementCanvas.Children.Add(CreateMarker(position, Brushes.Cyan));

            UpdateMagnifier();
            CaptureMouse();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_viewModel.IsDrawing)
                return;

            var position = e.GetPosition(MeasurementCanvas);
            _viewModel.UpdateMeasurement(ToPhysical(position));

            UpdateMagnifier();

            if (_measurementLine != null)
            {
                _measurementLine.X2 = position.X;
                _measurementLine.Y2 = position.Y;
            }

            // Move the existing marker instead of destroying and rebuilding one per mouse move.
            if (_endMarker == null)
            {
                _endMarker = CreateMarker(position, Brushes.Cyan);
                MeasurementCanvas.Children.Add(_endMarker);
            }
            else
            {
                PositionMarker(_endMarker, position);
            }

            UpdateDistanceLabel(position);
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || !_viewModel.IsDrawing)
                return;

            ReleaseMouseCapture();
            _viewModel.EndMeasurement();
            UpdateDistanceLabel(e.GetPosition(MeasurementCanvas));
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                // Ctrl+M matches the ruler; plain M is convenient with a mouse in hand.
                case Key.M:
                    if (MagnifierPopup.IsOpen)
                        HideMagnifier();
                    else
                        ShowMagnifier();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    Close();
                    e.Handled = true;
                    break;

                // Clears the current line without leaving the mode.
                case Key.Delete:
                case Key.Back:
                    _viewModel.Clear();
                    ClearDrawing();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Builds an open reticle for an endpoint: a hollow circle with four ticks aimed at
        /// the centre, and nothing drawn at the centre itself.
        ///
        /// A filled dot covers the very pixel the user is trying to place, which makes the
        /// exact endpoint guesswork. Leaving the middle clear means the target stays
        /// readable right down to the pixel under magnification.
        /// </summary>
        private static Path CreateMarker(Point center, Brush stroke)
        {
            var geometry = new GeometryGroup();
            geometry.Children.Add(new EllipseGeometry(new Point(0, 0), MarkerRadius, MarkerRadius));

            // Ticks stop short of the centre, leaving MarkerCentreGap clear all round.
            geometry.Children.Add(new LineGeometry(new Point(-MarkerTickLength, 0), new Point(-MarkerCentreGap, 0)));
            geometry.Children.Add(new LineGeometry(new Point(MarkerCentreGap, 0), new Point(MarkerTickLength, 0)));
            geometry.Children.Add(new LineGeometry(new Point(0, -MarkerTickLength), new Point(0, -MarkerCentreGap)));
            geometry.Children.Add(new LineGeometry(new Point(0, MarkerCentreGap), new Point(0, MarkerTickLength)));
            geometry.Freeze();

            var marker = new Path
            {
                Data = geometry,
                Stroke = stroke,
                StrokeThickness = MarkerStrokeThickness,
                // The overlay handles the drag itself; markers must not intercept it.
                IsHitTestVisible = false,
                // Keeps the reticle legible over both light and dark content.
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 3,
                    ShadowDepth = 0,
                    Opacity = 0.9
                },
                RenderTransform = new TranslateTransform(center.X, center.Y)
            };

            return marker;
        }

        /// <summary>
        /// Moves an existing reticle, rather than rebuilding its geometry each mouse move.
        /// </summary>
        private static void PositionMarker(Path marker, Point center)
        {
            if (marker.RenderTransform is TranslateTransform transform)
            {
                transform.X = center.X;
                transform.Y = center.Y;
            }
        }

        /// <summary>
        /// Shows the current distance near the midpoint of the line.
        /// </summary>
        /// <param name="currentPosition">Cursor position in canvas coordinates.</param>
        private void UpdateDistanceLabel(Point currentPosition)
        {
            if (!_viewModel.HasMeasurement || _measurementLine == null)
                return;

            if (_distanceLabel == null)
            {
                _distanceLabel = new TextBlock
                {
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                    Padding = new Thickness(8, 4, 8, 4),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold
                };
                MeasurementCanvas.Children.Add(_distanceLabel);
            }

            _distanceLabel.Text = _viewModel.Distance;

            // Position from the canvas coordinates of the line itself, not from the
            // ViewModel's physical-pixel points, which are in a different coordinate space.
            var midX = (_measurementLine.X1 + currentPosition.X) / 2;
            var midY = (_measurementLine.Y1 + currentPosition.Y) / 2;

            Canvas.SetLeft(_distanceLabel, midX + LabelOffsetX);
            Canvas.SetTop(_distanceLabel, midY + LabelOffsetY);
        }

        #region Magnifier

        private void ShowMagnifier()
        {
            if (!MagnifierPopup.IsOpen)
            {
                MagnifierPopup.IsOpen = true;
                Magnifier.Start();
            }

            UpdateMagnifier();
        }

        private void HideMagnifier()
        {
            if (!MagnifierPopup.IsOpen)
                return;

            MagnifierPopup.IsOpen = false;
            Magnifier.Stop();
        }

        /// <summary>
        /// Keeps the magnifier parked away from the cursor and captioned with the
        /// distance being measured.
        /// </summary>
        private void UpdateMagnifier()
        {
            if (!MagnifierPopup.IsOpen)
                return;

            var cursorScreen = PointToScreen(Mouse.GetPosition(this));

            var (x, y) = ScreenHelper.GetOverlayCornerPosition(
                cursorScreen.X, cursorScreen.Y,
                RulerDefaults.MagnifierSize, RulerDefaults.MagnifierMargin, _dpiScale.X);

            MagnifierPopup.HorizontalOffset = x;
            MagnifierPopup.VerticalOffset = y;

            Magnifier.SetCaption(_viewModel.HasMeasurement ? _viewModel.Distance : null);
        }

        #endregion

        private void ClearDrawing()
        {
            MeasurementCanvas.Children.Clear();
            _measurementLine = null;
            _endMarker = null;
            _distanceLabel = null;
        }
    }
}

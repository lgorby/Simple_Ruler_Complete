using RulerOverlay.Helpers;
using RulerOverlay.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
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
        private const double MarkerRadius = 6;
        private const double LabelOffsetX = -40;
        private const double LabelOffsetY = -20;

        private readonly PointToPointViewModel _viewModel;

        private Line? _measurementLine;
        private Ellipse? _endMarker;
        private TextBlock? _distanceLabel;

        /// <summary>
        /// Scale between WPF device-independent pixels and physical screen pixels.
        /// Captured once the window has a presentation source.
        /// </summary>
        private (double X, double Y) _dpiScale = (1.0, 1.0);

        public PointToPointWindow(PointToPointViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;

            Loaded += PointToPointWindow_Loaded;
        }

        private void PointToPointWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _dpiScale = ScreenHelper.GetDpiScale(this);

            CoverVirtualDesktop();

            // The overlay must have focus for Esc to close it.
            Activate();
            Focus();
        }

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

            CaptureMouse();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_viewModel.IsDrawing)
                return;

            var position = e.GetPosition(MeasurementCanvas);
            _viewModel.UpdateMeasurement(ToPhysical(position));

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

        private static Ellipse CreateMarker(Point center, Brush fill)
        {
            var marker = new Ellipse
            {
                Width = MarkerRadius * 2,
                Height = MarkerRadius * 2,
                Fill = fill,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            PositionMarker(marker, center);
            return marker;
        }

        private static void PositionMarker(Ellipse marker, Point center)
        {
            Canvas.SetLeft(marker, center.X - MarkerRadius);
            Canvas.SetTop(marker, center.Y - MarkerRadius);
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

        private void ClearDrawing()
        {
            MeasurementCanvas.Children.Clear();
            _measurementLine = null;
            _endMarker = null;
            _distanceLabel = null;
        }
    }
}

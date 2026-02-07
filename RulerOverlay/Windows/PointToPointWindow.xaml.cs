using RulerOverlay.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace RulerOverlay.Windows
{
    public partial class PointToPointWindow : Window
    {
        private readonly PointToPointViewModel _viewModel;
        private Line? _measurementLine;
        private Ellipse? _startCircle;
        private Ellipse? _endCircle;
        private TextBlock? _distanceLabel;

        public PointToPointWindow(PointToPointViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var position = e.GetPosition(this);
                _viewModel.StartMeasurement(position);

                // Clear previous drawing
                ClearDrawing();

                // Create new line
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

                // Create start circle
                _startCircle = CreateCircle(position, Brushes.Cyan);
                MeasurementCanvas.Children.Add(_startCircle);
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _viewModel.IsDrawing)
            {
                var position = e.GetPosition(this);
                _viewModel.UpdateMeasurement(position);

                if (_measurementLine != null)
                {
                    _measurementLine.X2 = position.X;
                    _measurementLine.Y2 = position.Y;
                }

                // Update or create end circle
                if (_endCircle != null)
                {
                    MeasurementCanvas.Children.Remove(_endCircle);
                }
                _endCircle = CreateCircle(position, Brushes.Cyan);
                MeasurementCanvas.Children.Add(_endCircle);

                // Update distance label
                UpdateDistanceLabel();
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _viewModel.IsDrawing)
            {
                _viewModel.EndMeasurement();
                UpdateDistanceLabel();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private Ellipse CreateCircle(Point center, Brush fill)
        {
            const double radius = 6;
            var circle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fill,
                Stroke = Brushes.White,
                StrokeThickness = 2
            };

            Canvas.SetLeft(circle, center.X - radius);
            Canvas.SetTop(circle, center.Y - radius);

            return circle;
        }

        private void UpdateDistanceLabel()
        {
            if (!_viewModel.HasMeasurement || _viewModel.StartPoint == null || _viewModel.CurrentPoint == null)
                return;

            // Remove old label
            if (_distanceLabel != null)
            {
                MeasurementCanvas.Children.Remove(_distanceLabel);
            }

            // Calculate midpoint
            var midX = (_viewModel.StartPoint.Value.X + _viewModel.CurrentPoint.Value.X) / 2;
            var midY = (_viewModel.StartPoint.Value.Y + _viewModel.CurrentPoint.Value.Y) / 2;

            // Create distance label
            _distanceLabel = new TextBlock
            {
                Text = _viewModel.Distance,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };

            Canvas.SetLeft(_distanceLabel, midX - 40); // Offset for centering
            Canvas.SetTop(_distanceLabel, midY - 20);

            MeasurementCanvas.Children.Add(_distanceLabel);
        }

        private void ClearDrawing()
        {
            MeasurementCanvas.Children.Clear();
            _measurementLine = null;
            _startCircle = null;
            _endCircle = null;
            _distanceLabel = null;
        }
    }
}

using RulerOverlay.Services;
using System;
using Point = System.Windows.Point;

namespace RulerOverlay.ViewModels
{
    /// <summary>
    /// ViewModel for point-to-point measurement mode
    /// Handles measurement line drawing and distance calculation
    /// </summary>
    public class PointToPointViewModel : ViewModelBase
    {
        private readonly MeasurementEngine _measurementEngine;
        private readonly string _unit;
        private readonly int _ppi;

        private Point? _startPoint;
        private Point? _currentPoint;
        private bool _isDrawing = false;

        public PointToPointViewModel(MeasurementEngine measurementEngine, string unit, int ppi)
        {
            _measurementEngine = measurementEngine;
            _unit = unit;
            _ppi = ppi;
        }

        #region Properties

        public Point? StartPoint
        {
            get => _startPoint;
            set
            {
                if (SetProperty(ref _startPoint, value))
                {
                    OnPropertyChanged(nameof(Distance));
                    OnPropertyChanged(nameof(HasMeasurement));
                }
            }
        }

        public Point? CurrentPoint
        {
            get => _currentPoint;
            set
            {
                if (SetProperty(ref _currentPoint, value))
                {
                    OnPropertyChanged(nameof(Distance));
                    OnPropertyChanged(nameof(HasMeasurement));
                }
            }
        }

        public bool IsDrawing
        {
            get => _isDrawing;
            set => SetProperty(ref _isDrawing, value);
        }

        public bool HasMeasurement => StartPoint.HasValue && CurrentPoint.HasValue;

        public string Distance
        {
            get
            {
                if (!StartPoint.HasValue || !CurrentPoint.HasValue)
                    return "";

                var dx = CurrentPoint.Value.X - StartPoint.Value.X;
                var dy = CurrentPoint.Value.Y - StartPoint.Value.Y;
                var pixels = Math.Sqrt(dx * dx + dy * dy);

                return _unit switch
                {
                    "inches" => $"{_measurementEngine.PixelsToInches(pixels):F2} in",
                    "centimeters" => $"{_measurementEngine.PixelsToCentimeters(pixels):F2} cm",
                    _ => $"{pixels:F0} px"
                };
            }
        }

        #endregion

        #region Methods

        public void StartMeasurement(Point point)
        {
            StartPoint = point;
            CurrentPoint = point;
            IsDrawing = true;
        }

        public void UpdateMeasurement(Point point)
        {
            if (IsDrawing)
            {
                CurrentPoint = point;
            }
        }

        public void EndMeasurement()
        {
            IsDrawing = false;
        }

        public void Clear()
        {
            StartPoint = null;
            CurrentPoint = null;
            IsDrawing = false;
        }

        #endregion
    }
}

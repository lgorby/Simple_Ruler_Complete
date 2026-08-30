using RulerOverlay.Models;
using RulerOverlay.Services;
using System;
using Point = System.Windows.Point;

namespace RulerOverlay.ViewModels
{
    /// <summary>
    /// ViewModel for point-to-point measurement mode.
    ///
    /// Points are held in physical screen pixels, not WPF device-independent pixels,
    /// so a distance reported as "px" is a real screen pixel count on scaled displays too.
    /// The view is responsible for converting mouse positions before handing them over.
    /// </summary>
    public class PointToPointViewModel : ViewModelBase
    {
        private readonly MeasurementEngine _measurementEngine;
        private readonly MeasurementUnit _unit;

        private Point? _startPoint;
        private Point? _currentPoint;
        private bool _isDrawing;

        public PointToPointViewModel(MeasurementEngine measurementEngine, MeasurementUnit unit)
        {
            _measurementEngine = measurementEngine ?? throw new ArgumentNullException(nameof(measurementEngine));
            _unit = unit;
        }

        #region Properties

        /// <summary>Where the drag began, in physical screen pixels.</summary>
        public Point? StartPoint
        {
            get => _startPoint;
            set
            {
                if (SetProperty(ref _startPoint, value))
                    OnMeasurementChanged();
            }
        }

        /// <summary>Current cursor position, in physical screen pixels.</summary>
        public Point? CurrentPoint
        {
            get => _currentPoint;
            set
            {
                if (SetProperty(ref _currentPoint, value))
                    OnMeasurementChanged();
            }
        }

        public bool IsDrawing
        {
            get => _isDrawing;
            set => SetProperty(ref _isDrawing, value);
        }

        public bool HasMeasurement => _startPoint.HasValue && _currentPoint.HasValue;

        /// <summary>Distance between the two points in physical pixels.</summary>
        public double DistanceInPixels
        {
            get
            {
                if (!_startPoint.HasValue || !_currentPoint.HasValue)
                    return 0;

                var dx = _currentPoint.Value.X - _startPoint.Value.X;
                var dy = _currentPoint.Value.Y - _startPoint.Value.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>Distance formatted in the active unit, e.g. "212 px".</summary>
        public string Distance => HasMeasurement
            ? _measurementEngine.Format(DistanceInPixels, _unit)
            : string.Empty;

        private void OnMeasurementChanged()
        {
            OnPropertyChanged(nameof(Distance));
            OnPropertyChanged(nameof(DistanceInPixels));
            OnPropertyChanged(nameof(HasMeasurement));
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
                CurrentPoint = point;
        }

        public void EndMeasurement() => IsDrawing = false;

        public void Clear()
        {
            IsDrawing = false;
            StartPoint = null;
            CurrentPoint = null;
        }

        #endregion
    }
}

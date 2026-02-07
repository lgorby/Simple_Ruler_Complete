using System;
using System.Windows;
using System.Windows.Media;

namespace RulerOverlay.ViewModels
{
    /// <summary>
    /// ViewModel for the calibration dialog
    /// Calculates PPI based on screen diagonal input
    /// </summary>
    public class CalibrationViewModel : ViewModelBase
    {
        private string _diagonalInput = "";
        private int _currentPpi;
        private string _errorMessage = "";
        private bool _hasError = false;

        public CalibrationViewModel(int currentPpi)
        {
            _currentPpi = currentPpi;

            // Pre-fill with calculated diagonal from current PPI
            var calculatedDiagonal = CalculateDiagonalFromPpi(currentPpi);
            if (calculatedDiagonal > 0)
            {
                _diagonalInput = calculatedDiagonal.ToString("F1");
            }
        }

        #region Properties

        public string DiagonalInput
        {
            get => _diagonalInput;
            set
            {
                if (SetProperty(ref _diagonalInput, value))
                {
                    ValidateInput();
                    OnPropertyChanged(nameof(CalculatedPpi));
                    OnPropertyChanged(nameof(CanCalibrate));
                }
            }
        }

        public int CurrentPpi
        {
            get => _currentPpi;
            set => SetProperty(ref _currentPpi, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public string CalculatedPpi
        {
            get
            {
                var ppi = CalculatePpi();
                return ppi > 0 ? ppi.ToString() : "--";
            }
        }

        public bool CanCalibrate => CalculatePpi() > 0 && !HasError;

        #endregion

        #region PPI Calculation

        /// <summary>
        /// Calculates PPI from screen diagonal input
        /// Formula: PPI = sqrt(width² + height²) / diagonal
        /// </summary>
        public int CalculatePpi()
        {
            if (!double.TryParse(_diagonalInput, out double diagonal))
            {
                return 0;
            }

            if (diagonal < 5 || diagonal > 100)
            {
                return 0;
            }

            try
            {
                // Get screen resolution
                var primaryScreen = SystemParameters.PrimaryScreenWidth;
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                // Apply DPI scaling to get actual pixel dimensions
                var dpiScale = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
                screenWidth *= dpiScale;
                screenHeight *= dpiScale;

                // Calculate diagonal in pixels: sqrt(width² + height²)
                var diagonalPixels = Math.Sqrt(Math.Pow(screenWidth, 2) + Math.Pow(screenHeight, 2));

                // Calculate PPI: diagonal_pixels / diagonal_inches
                var ppi = (int)Math.Round(diagonalPixels / diagonal);

                return ppi;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Calculates screen diagonal from current PPI (for pre-filling input)
        /// </summary>
        private double CalculateDiagonalFromPpi(int ppi)
        {
            if (ppi <= 0) return 0;

            try
            {
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                var dpiScale = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
                screenWidth *= dpiScale;
                screenHeight *= dpiScale;

                var diagonalPixels = Math.Sqrt(Math.Pow(screenWidth, 2) + Math.Pow(screenHeight, 2));
                return diagonalPixels / ppi;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Validation

        private void ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(_diagonalInput))
            {
                ErrorMessage = "";
                HasError = false;
                return;
            }

            if (!double.TryParse(_diagonalInput, out double diagonal))
            {
                ErrorMessage = "Please enter a valid number";
                HasError = true;
                return;
            }

            if (diagonal < 5 || diagonal > 100)
            {
                ErrorMessage = "Screen diagonal must be between 5 and 100 inches";
                HasError = true;
                return;
            }

            ErrorMessage = "";
            HasError = false;
        }

        #endregion
    }
}

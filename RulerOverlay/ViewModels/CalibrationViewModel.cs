using RulerOverlay.Helpers;
using RulerOverlay.Models;
using System;
using System.Globalization;

namespace RulerOverlay.ViewModels
{
    /// <summary>
    /// ViewModel for the calibration dialog.
    ///
    /// PPI is derived from the monitor's true pixel diagonal and the physical diagonal the
    /// user reports: PPI = sqrt(width² + height²) / diagonalInches. The pixel dimensions come
    /// from <see cref="ScreenHelper"/>, which reports real device pixels — WPF's own
    /// SystemParameters values are device-independent and would give a scaling-dependent
    /// answer on a monitor that is not at 100%.
    /// </summary>
    public class CalibrationViewModel : ViewModelBase
    {
        private string _diagonalInput = "";
        private int _currentPpi;
        private string _errorMessage = "";
        private bool _hasError;

        public CalibrationViewModel(int currentPpi)
        {
            _currentPpi = currentPpi;

            // Pre-fill with the diagonal implied by the current PPI, so re-opening the
            // dialog shows what was entered last time.
            var diagonal = DiagonalFromPpi(currentPpi);
            if (diagonal > 0)
                _diagonalInput = diagonal.ToString("F1", CultureInfo.CurrentCulture);
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
            private set => SetProperty(ref _errorMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            private set => SetProperty(ref _hasError, value);
        }

        /// <summary>PPI implied by the current input, or "--" when the input is unusable.</summary>
        public string CalculatedPpi
        {
            get
            {
                var ppi = CalculatePpi();
                return ppi > 0 ? ppi.ToString(CultureInfo.CurrentCulture) : "--";
            }
        }

        public bool CanCalibrate => !HasError && CalculatePpi() > 0;

        #endregion

        #region PPI Calculation

        /// <summary>
        /// PPI for the entered diagonal, or 0 when the input is not a usable measurement.
        /// </summary>
        public int CalculatePpi()
        {
            if (!TryParseDiagonal(_diagonalInput, out double diagonalInches))
                return 0;

            var diagonalPixels = ScreenHelper.GetPrimaryScreenDiagonalPixels();
            if (diagonalPixels <= 0)
                return 0;

            var ppi = (int)Math.Round(diagonalPixels / diagonalInches);

            // Refuse a result the rest of the app would reject anyway.
            return ppi >= RulerDefaults.MinPpi && ppi <= RulerDefaults.MaxPpi ? ppi : 0;
        }

        /// <summary>
        /// Inverse of <see cref="CalculatePpi"/>: the diagonal a given PPI implies.
        /// </summary>
        private static double DiagonalFromPpi(int ppi)
        {
            if (ppi <= 0)
                return 0;

            return ScreenHelper.GetPrimaryScreenDiagonalPixels() / ppi;
        }

        /// <summary>
        /// Parses the diagonal, accepting either the user's locale decimal separator or
        /// a plain '.', so "24.5" works on locales that use a comma and vice versa.
        /// </summary>
        private static bool TryParseDiagonal(string? input, out double diagonalInches)
        {
            diagonalInches = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            const NumberStyles styles = NumberStyles.Float;

            if (!double.TryParse(input, styles, CultureInfo.CurrentCulture, out diagonalInches) &&
                !double.TryParse(input, styles, CultureInfo.InvariantCulture, out diagonalInches))
            {
                return false;
            }

            return diagonalInches >= RulerDefaults.MinScreenDiagonalInches &&
                   diagonalInches <= RulerDefaults.MaxScreenDiagonalInches;
        }

        #endregion

        #region Validation

        private void ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(_diagonalInput))
            {
                SetValidation("", false);
                return;
            }

            const NumberStyles styles = NumberStyles.Float;
            bool parsed = double.TryParse(_diagonalInput, styles, CultureInfo.CurrentCulture, out double diagonal) ||
                          double.TryParse(_diagonalInput, styles, CultureInfo.InvariantCulture, out diagonal);

            if (!parsed)
            {
                SetValidation("Please enter a valid number", true);
                return;
            }

            if (diagonal < RulerDefaults.MinScreenDiagonalInches ||
                diagonal > RulerDefaults.MaxScreenDiagonalInches)
            {
                SetValidation(
                    $"Screen diagonal must be between {RulerDefaults.MinScreenDiagonalInches:F0} " +
                    $"and {RulerDefaults.MaxScreenDiagonalInches:F0} inches",
                    true);
                return;
            }

            if (CalculatePpi() <= 0)
            {
                SetValidation(
                    $"That diagonal implies a PPI outside the supported range " +
                    $"({RulerDefaults.MinPpi}-{RulerDefaults.MaxPpi})",
                    true);
                return;
            }

            SetValidation("", false);
        }

        private void SetValidation(string message, bool isError)
        {
            ErrorMessage = message;
            HasError = isError;
        }

        #endregion
    }
}

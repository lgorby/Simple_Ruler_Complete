using RulerOverlay.ViewModels;
using System.Windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;

namespace RulerOverlay.Windows
{
    public partial class CalibrationDialog : Window
    {
        private readonly CalibrationViewModel _viewModel;

        public int CalibratedPpi { get; private set; }

        public CalibrationDialog(int currentPpi)
        {
            InitializeComponent();

            _viewModel = new CalibrationViewModel(currentPpi);
            DataContext = _viewModel;

            // Focus the input box when dialog opens
            Loaded += (s, e) => DiagonalTextBox.Focus();

            // Allow Enter key to calibrate
            DiagonalTextBox.KeyDown += DiagonalTextBox_KeyDown;
        }

        private void DiagonalTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _viewModel.CanCalibrate)
            {
                ApplyCalibration();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyCalibration();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ApplyCalibration()
        {
            var calculatedPpi = _viewModel.CalculatePpi();
            if (calculatedPpi > 0)
            {
                CalibratedPpi = calculatedPpi;
                DialogResult = true;
                Close();
            }
        }
    }
}

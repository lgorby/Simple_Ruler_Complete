using RulerOverlay.ViewModels;
using System.Windows;

namespace RulerOverlay.Windows
{
    /// <summary>
    /// Asks the user for their screen's physical diagonal and derives a PPI from it.
    /// </summary>
    public partial class CalibrationDialog : Window
    {
        private readonly CalibrationViewModel _viewModel;

        /// <summary>
        /// The PPI to apply. Only meaningful when ShowDialog returned true.
        /// </summary>
        public int CalibratedPpi { get; private set; }

        public CalibrationDialog(int currentPpi)
        {
            InitializeComponent();

            _viewModel = new CalibrationViewModel(currentPpi);
            DataContext = _viewModel;

            // Enter and Esc are handled by the buttons' IsDefault/IsCancel, which work
            // regardless of which control has focus.
            Loaded += (_, _) =>
            {
                DiagonalTextBox.Focus();
                DiagonalTextBox.SelectAll();
            };
        }

        private void CalibrateButton_Click(object sender, RoutedEventArgs e)
        {
            var calculatedPpi = _viewModel.CalculatePpi();
            if (calculatedPpi <= 0)
                return;

            CalibratedPpi = calculatedPpi;
            DialogResult = true;
        }
    }
}

using System;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace RulerOverlay.Controls
{
    /// <summary>
    /// Transient confirmation banner, e.g. after copying a measurement.
    /// </summary>
    public partial class ToastControl : UserControl
    {
        private Storyboard? _storyboard;

        /// <summary>
        /// Raised once the toast has finished fading out, so a host can hide or
        /// close whatever is presenting it.
        /// </summary>
        public event EventHandler? Dismissed;

        public ToastControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows a message, fading it in and back out. Calling this again while a toast
        /// is on screen restarts the animation with the new message.
        /// </summary>
        public void ShowMessage(string message)
        {
            MessageText.Text = message;

            if (_storyboard == null)
            {
                _storyboard = (Storyboard)Resources["ToastStoryboard"];
                _storyboard.Completed += (_, _) => Dismissed?.Invoke(this, EventArgs.Empty);
            }

            // Restart rather than layer a second animation over the first.
            _storyboard.Stop(this);
            _storyboard.Begin(this, isControllable: true);
        }
    }
}

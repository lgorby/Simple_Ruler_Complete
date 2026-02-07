using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace RulerOverlay.Controls
{
    public partial class ToastControl : System.Windows.Controls.UserControl
    {
        public ToastControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Shows the toast with a message
        /// </summary>
        public void Show(string message)
        {
            MessageText.Text = message;

            // Get storyboards
            var fadeIn = (Storyboard)Resources["FadeInStoryboard"];
            var fadeOut = (Storyboard)Resources["FadeOutStoryboard"];

            // Stop any existing animations
            fadeIn.Stop(this);
            fadeOut.Stop(this);

            // Start fade in animation
            fadeIn.Begin(this);

            // Start fade out animation (will wait 1.7s before fading out)
            fadeOut.Begin(this);
        }
    }
}

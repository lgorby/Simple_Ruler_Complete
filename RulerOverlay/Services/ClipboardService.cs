using System.Windows;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Service for clipboard operations
    /// Copies formatted measurements to Windows clipboard
    /// </summary>
    public class ClipboardService
    {
        /// <summary>
        /// Copies text to Windows clipboard
        /// </summary>
        public bool CopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formats a measurement value based on unit
        /// </summary>
        public string FormatMeasurement(double pixels, string unit, int ppi)
        {
            var measurementEngine = new MeasurementEngine(ppi);

            return unit switch
            {
                "inches" => $"{measurementEngine.PixelsToInches(pixels):F2} in",
                "centimeters" => $"{measurementEngine.PixelsToCentimeters(pixels):F2} cm",
                _ => $"{pixels:F0} px"
            };
        }
    }
}

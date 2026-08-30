using RulerOverlay.Models;
using System;
using System.Globalization;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Result of a measurement calculation.
    /// </summary>
    public class MeasurementResult
    {
        public double Value { get; init; }
        public MeasurementUnit Unit { get; init; }
        public string Formatted { get; init; } = "";
    }

    /// <summary>
    /// Unit conversion and measurement formatting.
    /// This is the only place a measurement is turned into display text, so the
    /// ruler, the magnifier, the clipboard and point-to-point mode all agree.
    /// </summary>
    public class MeasurementEngine
    {
        private double _ppi;

        public MeasurementEngine(double ppi = RulerDefaults.Ppi)
        {
            Ppi = ppi;
        }

        /// <summary>
        /// Pixels per inch used for physical-unit conversions.
        /// Non-positive values are rejected so a bad calibration cannot produce
        /// infinite or negative measurements.
        /// </summary>
        public double Ppi
        {
            get => _ppi;
            set => _ppi = value > 0 ? value : RulerDefaults.Ppi;
        }

        public double PixelsToInches(double pixels) => pixels / _ppi;

        public double PixelsToCentimeters(double pixels) => (pixels / _ppi) * MeasurementUnits.CentimetersPerInch;

        /// <summary>
        /// Converts a pixel length into the requested unit's numeric value.
        /// </summary>
        public double ToUnit(double pixels, MeasurementUnit unit) => unit switch
        {
            MeasurementUnit.Inches => PixelsToInches(pixels),
            MeasurementUnit.Centimeters => PixelsToCentimeters(pixels),
            _ => pixels
        };

        /// <summary>
        /// Converts a pixel length and formats it for display, e.g. "12.70 cm".
        /// </summary>
        public MeasurementResult Convert(double pixels, MeasurementUnit unit)
        {
            var value = ToUnit(pixels, unit);

            return new MeasurementResult
            {
                Value = value,
                Unit = unit,
                Formatted = Format(pixels, unit)
            };
        }

        /// <summary>
        /// Formats a pixel length in the given unit using the user's locale for the
        /// number and the unit's own precision, e.g. "500 px" / "5.21 in".
        /// </summary>
        public string Format(double pixels, MeasurementUnit unit)
        {
            var value = ToUnit(pixels, unit);
            return FormatValue(value, unit);
        }

        /// <summary>
        /// Formats an already-converted value in the given unit.
        /// </summary>
        public static string FormatValue(double value, MeasurementUnit unit)
        {
            var text = value.ToString("F" + unit.Precision().ToString(CultureInfo.InvariantCulture),
                                      CultureInfo.CurrentCulture);
            return $"{text} {unit.Abbreviation()}";
        }

        /// <summary>
        /// Formats a position together with the total it sits within, e.g. "1750 / 1755 px".
        /// Used by the magnifier, where showing the cursor position alone is ambiguous
        /// against the ruler's own length caption.
        /// </summary>
        public string FormatWithTotal(double pixels, double totalPixels, MeasurementUnit unit)
        {
            var format = "F" + unit.Precision().ToString(CultureInfo.InvariantCulture);
            var position = ToUnit(pixels, unit).ToString(format, CultureInfo.CurrentCulture);
            var total = ToUnit(totalPixels, unit).ToString(format, CultureInfo.CurrentCulture);

            return $"{position} / {total} {unit.Abbreviation()}";
        }

        /// <summary>
        /// Straight-line distance between two points, formatted in the given unit.
        /// </summary>
        public MeasurementResult CalculateDistance(double x1, double y1, double x2, double y2, MeasurementUnit unit)
        {
            var dx = x2 - x1;
            var dy = y2 - y1;
            return Convert(Math.Sqrt(dx * dx + dy * dy), unit);
        }
    }
}

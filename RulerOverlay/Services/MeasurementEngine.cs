using System;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Result of a measurement calculation
    /// </summary>
    public class MeasurementResult
    {
        public double Value { get; set; }
        public string Unit { get; set; } = "pixels";
        public string Formatted { get; set; } = "";
    }

    /// <summary>
    /// Service for unit conversions and measurement calculations
    /// Direct port from src/services/MeasurementEngine.ts
    /// </summary>
    public class MeasurementEngine
    {
        private double _ppi; // Pixels Per Inch

        public MeasurementEngine(double ppi = 96)
        {
            _ppi = ppi;
        }

        public void SetPPI(double ppi)
        {
            _ppi = ppi;
        }

        public double GetPPI()
        {
            return _ppi;
        }

        public double PixelsToInches(double pixels)
        {
            return pixels / _ppi;
        }

        public double PixelsToCentimeters(double pixels)
        {
            return (pixels / _ppi) * 2.54;
        }

        public double InchesToPixels(double inches)
        {
            return inches * _ppi;
        }

        public double CentimetersToPixels(double cm)
        {
            return (cm / 2.54) * _ppi;
        }

        public MeasurementResult Convert(double pixels, string targetUnit)
        {
            double value;

            switch (targetUnit.ToLower())
            {
                case "inches":
                    value = PixelsToInches(pixels);
                    break;
                case "centimeters":
                    value = PixelsToCentimeters(pixels);
                    break;
                case "pixels":
                default:
                    value = pixels;
                    break;
            }

            return new MeasurementResult
            {
                Value = value,
                Unit = targetUnit,
                Formatted = FormatMeasurement(value, targetUnit)
            };
        }

        private string FormatMeasurement(double value, string unit)
        {
            var abbreviations = new System.Collections.Generic.Dictionary<string, string>
            {
                { "pixels", "px" },
                { "inches", "in" },
                { "centimeters", "cm" }
            };

            var precision = unit.ToLower() == "pixels" ? 0 : 2;
            var abbr = abbreviations.ContainsKey(unit.ToLower()) ? abbreviations[unit.ToLower()] : "px";

            return $"{value.ToString($"F{precision}")}{abbr}";
        }

        public MeasurementResult CalculateDistance(
            double x1,
            double y1,
            double x2,
            double y2,
            string unit = "pixels")
        {
            var pixelDistance = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
            return Convert(pixelDistance, unit);
        }
    }
}

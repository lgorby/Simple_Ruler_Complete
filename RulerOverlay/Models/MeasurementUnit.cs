using System;
using System.Collections.Generic;

namespace RulerOverlay.Models
{
    /// <summary>
    /// Measurement unit for ruler display.
    /// This enum is the single source of truth for units; the string forms used in
    /// the config file and in XAML command parameters are produced by <see cref="MeasurementUnits"/>.
    /// </summary>
    public enum MeasurementUnit
    {
        Pixels = 0,
        Inches = 1,
        Centimeters = 2
    }

    /// <summary>
    /// Conversion helpers between <see cref="MeasurementUnit"/> and its persisted/UI string form,
    /// plus the display metadata (abbreviation, decimal precision) for each unit.
    /// </summary>
    public static class MeasurementUnits
    {
        /// <summary>Centimeters in one inch. Exact by definition.</summary>
        public const double CentimetersPerInch = 2.54;

        private static readonly IReadOnlyDictionary<MeasurementUnit, string> Keys =
            new Dictionary<MeasurementUnit, string>
            {
                [MeasurementUnit.Pixels] = "pixels",
                [MeasurementUnit.Inches] = "inches",
                [MeasurementUnit.Centimeters] = "centimeters"
            };

        private static readonly IReadOnlyDictionary<MeasurementUnit, string> Abbreviations =
            new Dictionary<MeasurementUnit, string>
            {
                [MeasurementUnit.Pixels] = "px",
                [MeasurementUnit.Inches] = "in",
                [MeasurementUnit.Centimeters] = "cm"
            };

        private static readonly IReadOnlyDictionary<MeasurementUnit, int> Precisions =
            new Dictionary<MeasurementUnit, int>
            {
                [MeasurementUnit.Pixels] = 0,
                [MeasurementUnit.Inches] = 2,
                [MeasurementUnit.Centimeters] = 2
            };

        /// <summary>The stable string key used in the config file and XAML CommandParameters.</summary>
        public static string ToKey(this MeasurementUnit unit) =>
            Keys.TryGetValue(unit, out var key) ? key : Keys[MeasurementUnit.Pixels];

        /// <summary>Short suffix shown next to a value, e.g. "px".</summary>
        public static string Abbreviation(this MeasurementUnit unit) =>
            Abbreviations.TryGetValue(unit, out var abbr) ? abbr : Abbreviations[MeasurementUnit.Pixels];

        /// <summary>Number of decimal places used when formatting this unit.</summary>
        public static int Precision(this MeasurementUnit unit) =>
            Precisions.TryGetValue(unit, out var precision) ? precision : 0;

        /// <summary>
        /// Parses a config/UI string key back to a unit. Unknown or malformed input
        /// falls back to <see cref="MeasurementUnit.Pixels"/> rather than throwing.
        /// </summary>
        public static MeasurementUnit Parse(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return MeasurementUnit.Pixels;

            foreach (var pair in Keys)
            {
                if (string.Equals(pair.Value, key, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }

            return MeasurementUnit.Pixels;
        }
    }
}

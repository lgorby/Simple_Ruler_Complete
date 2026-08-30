using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace RulerOverlay.Models
{
    /// <summary>
    /// The ruler's selectable background colours.
    /// Single source of truth for the colour names used in the config file, in the
    /// context menu's CommandParameters, and when building the background brush.
    /// </summary>
    public static class RulerColors
    {
        private static readonly IReadOnlyDictionary<string, Color> Palette =
            new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
            {
                ["white"] = Colors.White,
                ["black"] = Colors.Black,
                ["yellow"] = Colors.Yellow,
                ["cyan"] = Colors.Cyan
            };

        /// <summary>Colour names in menu order.</summary>
        public static IEnumerable<string> Names => Palette.Keys;

        /// <summary>True when the name maps to a supported colour.</summary>
        public static bool IsKnown(string? name) => name != null && Palette.ContainsKey(name);

        /// <summary>
        /// Resolves a colour name, falling back to the default for anything unrecognised.
        /// </summary>
        public static Color Resolve(string? name)
        {
            if (name != null && Palette.TryGetValue(name, out var color))
                return color;

            return Palette[RulerDefaults.Color];
        }

        /// <summary>
        /// Builds a frozen brush for a colour name at the given opacity.
        /// Freezing lets WPF share the brush across threads and skip change tracking.
        /// </summary>
        public static Brush CreateBrush(string? name, double opacity)
        {
            var brush = new SolidColorBrush(Resolve(name)) { Opacity = opacity };
            brush.Freeze();
            return brush;
        }
    }
}

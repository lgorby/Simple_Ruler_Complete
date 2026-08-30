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

        // Ink is chosen from the background's brightness rather than hard-coded, so a new
        // ruler colour automatically gets readable markings.
        private static readonly Color DarkInk = Color.FromRgb(0x1A, 0x1A, 0x1A);
        private static readonly Color LightInk = Color.FromRgb(0xE8, 0xE8, 0xE8);
        private static readonly Color DarkAccent = Colors.DarkSlateGray;
        private static readonly Color LightAccent = Color.FromRgb(0xA8, 0xC8, 0xC8);

        /// <summary>
        /// Perceived brightness of a colour, 0 (black) to 1 (white).
        /// Weighted for how the eye responds to each channel, so yellow and cyan count as
        /// light even though their raw averages are middling.
        /// </summary>
        private static double Brightness(Color color) =>
            (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

        /// <summary>True when a ruler colour needs light markings drawn on it.</summary>
        public static bool IsDark(string? name) => Brightness(Resolve(name)) < 0.5;

        /// <summary>
        /// Colour for tick marks and their labels: near-black on a light ruler, a soft
        /// off-white on a dark one. Pure white is avoided because it glares against the
        /// dark background at full opacity.
        /// </summary>
        public static Brush CreateInkBrush(string? name) =>
            Frozen(IsDark(name) ? LightInk : DarkInk);

        /// <summary>
        /// Slightly muted colour used for the overall-length caption, so it reads as
        /// secondary to the tick labels while staying legible on either background.
        /// </summary>
        public static Brush CreateAccentBrush(string? name) =>
            Frozen(IsDark(name) ? LightAccent : DarkAccent);

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
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

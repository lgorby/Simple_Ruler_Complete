using Newtonsoft.Json;
using System.Collections.Generic;

namespace RulerOverlay.Models
{
    /// <summary>
    /// Configuration model for the ruler overlay application.
    /// Persisted to %APPDATA%\RulerOverlay\config.json.
    /// Every default here comes from <see cref="RulerDefaults"/>.
    /// </summary>
    public class RulerConfig
    {
        [JsonProperty("position")]
        public Position Position { get; set; } = new Position();

        [JsonProperty("size")]
        public Size Size { get; set; } = new Size { Width = RulerDefaults.Width, Height = RulerDefaults.Height };

        [JsonProperty("rotation")]
        public int Rotation { get; set; } = RulerDefaults.Rotation;

        [JsonProperty("unit")]
        public string Unit { get; set; } = RulerDefaults.Unit.ToKey();

        [JsonProperty("opacity")]
        public int Opacity { get; set; } = RulerDefaults.Opacity;

        [JsonProperty("color")]
        public string Color { get; set; } = RulerDefaults.Color;

        [JsonProperty("ppi")]
        public int Ppi { get; set; } = RulerDefaults.Ppi;

        [JsonProperty("magnifierZoom")]
        public int MagnifierZoom { get; set; } = RulerDefaults.MagnifierZoom;

        [JsonProperty("magnifierEnabled")]
        public bool MagnifierEnabled { get; set; }

        [JsonProperty("edgeSnappingEnabled")]
        public bool EdgeSnappingEnabled { get; set; }

        [JsonProperty("clickThroughEnabled")]
        public bool ClickThroughEnabled { get; set; }

        /// <summary>
        /// Shortcut map. Not currently user-editable through the UI, but it is round-tripped
        /// so hand-edits to config.json survive a save.
        /// </summary>
        [JsonProperty("shortcuts")]
        public Dictionary<string, string> Shortcuts { get; set; } = DefaultShortcuts();

        public static Dictionary<string, string> DefaultShortcuts() => new()
        {
            { "reset", "Ctrl+R" },
            { "copy", "Ctrl+C" },
            { "toggleTransparency", "Ctrl+T" },
            { "toggleMagnifier", "Ctrl+M" },
            { "toggleSnapping", "Ctrl+S" },
            { "clickThrough", "Ctrl+K" },
            { "pointToPoint", "Ctrl+P" },
            { "clearGuides", "Ctrl+G" },
            { "quit", "Ctrl+Q" },
            { "help", "F1" }
        };

        /// <summary>
        /// Creates a default configuration instance.
        /// </summary>
        public static RulerConfig Default => new();

        /// <summary>
        /// Forces every field into a usable range, replacing missing or nonsensical
        /// values with defaults. Applied to anything read from disk so a corrupt or
        /// hand-edited config can never put the ruler into an unusable state.
        /// </summary>
        public RulerConfig Sanitized()
        {
            var defaults = Default;

            return new RulerConfig
            {
                Position = Position ?? defaults.Position,
                Size = new Size
                {
                    Width = RulerDefaults.Clamp(Size?.Width ?? RulerDefaults.Width,
                                                RulerDefaults.MinWidth, RulerDefaults.MaxWidth),
                    Height = RulerDefaults.Clamp(Size?.Height ?? RulerDefaults.Height,
                                                 RulerDefaults.MinHeight, RulerDefaults.MaxHeight)
                },
                Rotation = RulerDefaults.NormalizeRotation(Rotation),
                // Round-trips through the enum, so an unknown unit string becomes "pixels".
                Unit = MeasurementUnits.Parse(Unit).ToKey(),
                Opacity = RulerDefaults.Clamp(Opacity, RulerDefaults.MinOpacity, RulerDefaults.MaxOpacity),
                Color = string.IsNullOrWhiteSpace(Color) ? defaults.Color : Color,
                Ppi = RulerDefaults.Clamp(Ppi, RulerDefaults.MinPpi, RulerDefaults.MaxPpi),
                MagnifierZoom = RulerDefaults.Clamp(MagnifierZoom,
                                                    RulerDefaults.MinMagnifierZoom, RulerDefaults.MaxMagnifierZoom),
                MagnifierEnabled = MagnifierEnabled,
                EdgeSnappingEnabled = EdgeSnappingEnabled,
                ClickThroughEnabled = ClickThroughEnabled,
                Shortcuts = Shortcuts is { Count: > 0 } ? Shortcuts : defaults.Shortcuts
            };
        }
    }

    /// <summary>
    /// Window position, in screen pixels.
    /// </summary>
    public class Position
    {
        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

    /// <summary>
    /// Ruler dimensions, in screen pixels.
    /// </summary>
    public class Size
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}

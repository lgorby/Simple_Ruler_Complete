using Newtonsoft.Json;
using System.Collections.Generic;

namespace RulerOverlay.Models
{
    /// <summary>
    /// Configuration model for the ruler overlay application
    /// Persisted to %APPDATA%\RulerOverlay\config.json
    /// </summary>
    public class RulerConfig
    {
        [JsonProperty("position")]
        public Position Position { get; set; } = new Position { X = 0, Y = 0 };

        [JsonProperty("size")]
        public Size Size { get; set; } = new Size { Width = 500, Height = 90 };

        [JsonProperty("rotation")]
        public int Rotation { get; set; } = 0;

        [JsonProperty("unit")]
        public string Unit { get; set; } = "pixels";

        [JsonProperty("opacity")]
        public int Opacity { get; set; } = 100;

        [JsonProperty("color")]
        public string Color { get; set; } = "white";

        [JsonProperty("ppi")]
        public int Ppi { get; set; } = 96;

        [JsonProperty("magnifierZoom")]
        public int MagnifierZoom { get; set; } = 4;

        [JsonProperty("magnifierEnabled")]
        public bool MagnifierEnabled { get; set; } = false;

        [JsonProperty("edgeSnappingEnabled")]
        public bool EdgeSnappingEnabled { get; set; } = false;

        [JsonProperty("shortcuts")]
        public Dictionary<string, string> Shortcuts { get; set; } = new Dictionary<string, string>
        {
            { "reset", "Ctrl+R" },
            { "copy", "Ctrl+C" },
            { "toggleTransparency", "Ctrl+T" },
            { "toggleMagnifier", "Ctrl+M" },
            { "toggleSnapping", "Ctrl+S" },
            { "pointToPoint", "Ctrl+P" },
            { "quit", "Ctrl+Q" },
            { "help", "F1" }
        };

        /// <summary>
        /// Creates a default configuration instance
        /// </summary>
        public static RulerConfig Default => new RulerConfig();
    }

    /// <summary>
    /// Window position coordinates
    /// </summary>
    public class Position
    {
        [JsonProperty("x")]
        public int X { get; set; }

        [JsonProperty("y")]
        public int Y { get; set; }
    }

    /// <summary>
    /// Window size dimensions
    /// </summary>
    public class Size
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}

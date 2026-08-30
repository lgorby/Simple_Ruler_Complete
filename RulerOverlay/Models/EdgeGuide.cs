namespace RulerOverlay.Models
{
    /// <summary>
    /// A vertical guide line drawn across the ruler, created by Shift+clicking it.
    /// </summary>
    public class EdgeGuide
    {
        /// <summary>X position in pixels, relative to the ruler's left edge.</summary>
        public double Position { get; }

        /// <summary>Caption shown beside the guide, e.g. "120 px".</summary>
        public string Label { get; }

        public EdgeGuide(double position, string label)
        {
            Position = position;
            Label = label ?? "";
        }
    }
}

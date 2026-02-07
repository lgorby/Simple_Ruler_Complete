namespace RulerOverlay.Models
{
    /// <summary>
    /// Represents a vertical edge guide line
    /// Created by Shift+drag on the ruler
    /// </summary>
    public class EdgeGuide
    {
        /// <summary>
        /// X position relative to ruler left edge
        /// </summary>
        public double Position { get; set; }

        /// <summary>
        /// Label to display on the guide
        /// </summary>
        public string Label { get; set; } = "";

        public EdgeGuide(double position, string label)
        {
            Position = position;
            Label = label;
        }
    }
}

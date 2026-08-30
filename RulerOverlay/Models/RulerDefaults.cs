using System;

namespace RulerOverlay.Models
{
    /// <summary>
    /// Single source of truth for every default and limit used by the ruler.
    /// Config defaults, ViewModel initial state, "reset to center" and the
    /// validation performed on a loaded config all read from here, so a value
    /// only ever needs changing in one place.
    /// </summary>
    public static class RulerDefaults
    {
        // --- Geometry -------------------------------------------------------
        public const int Width = 500;
        public const int Height = 90;

        /// <summary>Smallest ruler length the user can drag down to.</summary>
        public const int MinWidth = 100;

        /// <summary>Upper bound guard so a corrupt config cannot request a gigantic window.</summary>
        public const int MaxWidth = 20000;

        public const int MinHeight = 40;
        public const int MaxHeight = 400;

        /// <summary>Ruler pixels that must remain on a monitor for the window to count as reachable.</summary>
        public const int MinVisiblePixels = 100;

        // --- Rotation -------------------------------------------------------
        public const int Rotation = 0;

        /// <summary>
        /// Preset angles offered in the context menu and stepped through by the
        /// quick-rotate button. Both read this list, so the button can never land on an
        /// angle the menu cannot show as checked.
        /// </summary>
        public static readonly int[] RotationPresets = { 0, 45, 90, 135, 180 };

        // --- Appearance -----------------------------------------------------
        public const int Opacity = 100;
        public const int MinOpacity = 20;
        public const int MaxOpacity = 100;

        /// <summary>Opacity steps cycled by Ctrl+T, in the order they are visited.</summary>
        public static readonly int[] OpacitySteps = { 100, 80, 60, 40, 20 };

        public const string Color = "white";

        // --- Measurement ----------------------------------------------------
        /// <summary>Windows' nominal DPI, used until the user calibrates.</summary>
        public const int Ppi = 96;

        public const int MinPpi = 30;
        public const int MaxPpi = 1000;

        public static readonly MeasurementUnit Unit = MeasurementUnit.Pixels;

        // --- Magnifier ------------------------------------------------------
        public const int MagnifierZoom = 4;
        public const int MinMagnifierZoom = 2;
        public const int MaxMagnifierZoom = 16;

        /// <summary>Edge of the magnifier window, in device-independent pixels.</summary>
        public const double MagnifierSize = 200;

        /// <summary>How close to a ruler edge the cursor must be for the magnifier to appear.</summary>
        public const double MagnifierEdgeThreshold = 15.0;

        /// <summary>Gap between the magnifier and the screen edge it parks against, in DIPs.</summary>
        public const double MagnifierMargin = 10.0;

        // --- Calibration ----------------------------------------------------
        public const double MinScreenDiagonalInches = 5;
        public const double MaxScreenDiagonalInches = 100;

        // --- Interaction ----------------------------------------------------
        /// <summary>Arrow-key nudge distance; Shift multiplies it.</summary>
        public const int NudgeStep = 1;
        public const int NudgeStepLarge = 10;

        /// <summary>Click tolerance for picking an existing edge guide to remove.</summary>
        public const double GuideHitTolerance = 6.0;

        /// <summary>
        /// Width of the drag-to-resize strip at each end of the ruler, in device-independent
        /// pixels. Chrome is specified in DIPs so it stays the same apparent size, unlike the
        /// ruler body which is deliberately measured in real screen pixels.
        /// </summary>
        public const double ResizeHandleWidth = 10.0;

        /// <summary>How long ruler changes are batched before the config file is rewritten.</summary>
        public static readonly TimeSpan ConfigSaveDebounce = TimeSpan.FromMilliseconds(400);

        /// <summary>Clamps a value into an inclusive range.</summary>
        public static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

        /// <summary>Normalizes any angle into the 0-359 range.</summary>
        public static int NormalizeRotation(int degrees) => ((degrees % 360) + 360) % 360;
    }
}

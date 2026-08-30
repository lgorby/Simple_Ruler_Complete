using CommunityToolkit.Mvvm.Input;
using RulerOverlay.Helpers;
using RulerOverlay.Models;
using RulerOverlay.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Drawing = System.Drawing;

namespace RulerOverlay.ViewModels
{
    /// <summary>
    /// ViewModel for the main ruler window. Owns all persisted ruler state.
    /// </summary>
    public class RulerViewModel : ViewModelBase, IDisposable
    {
        private readonly ConfigurationService _configService;
        private readonly MeasurementEngine _measurementEngine;

        /// <summary>
        /// Config writes are batched behind this timer. Dragging or nudging the ruler
        /// changes position on every mouse move; without debouncing that would rewrite
        /// config.json hundreds of times a second.
        /// </summary>
        private readonly DispatcherTimer _saveTimer;

        private bool _isLoading;
        private bool _savePending;
        private bool _disposed;

        private Brush? _cachedBackgroundBrush;

        /// <summary>Raised after a measurement has been placed on the clipboard.</summary>
        public event EventHandler<string>? MeasurementCopied;

        private int _width = RulerDefaults.Width;
        private int _height = RulerDefaults.Height;
        private int _positionX;
        private int _positionY;
        private int _rotation = RulerDefaults.Rotation;
        private int _opacity = RulerDefaults.Opacity;
        private string _color = RulerDefaults.Color;
        private MeasurementUnit _unit = RulerDefaults.Unit;
        private int _ppi = RulerDefaults.Ppi;
        private int _magnifierZoom = RulerDefaults.MagnifierZoom;
        private bool _magnifierEnabled;
        private bool _edgeSnappingEnabled;
        private bool _clickThroughEnabled;
        private double _pixelScale = 1.0;

        public ObservableCollection<EdgeGuide> EdgeGuides { get; } = new();

        public RulerViewModel(ConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _measurementEngine = new MeasurementEngine(_ppi);

            _saveTimer = new DispatcherTimer { Interval = RulerDefaults.ConfigSaveDebounce };
            _saveTimer.Tick += (_, _) => FlushPendingSave();

            SetRotationCommand = new RelayCommand<string>(value =>
            {
                if (int.TryParse(value, out int angle))
                    Rotation = angle;
            });

            SetUnitCommand = new RelayCommand<string>(value => Unit = MeasurementUnits.Parse(value));

            SetOpacityCommand = new RelayCommand<string>(value =>
            {
                if (int.TryParse(value, out int percent))
                    Opacity = percent;
            });

            SetColorCommand = new RelayCommand<string>(value =>
            {
                if (RulerColors.IsKnown(value))
                    Color = value!;
            });

            ResetPositionCommand = new RelayCommand(ResetPosition);
            ToggleMagnifierCommand = new RelayCommand(() => MagnifierEnabled = !MagnifierEnabled);
            ToggleEdgeSnappingCommand = new RelayCommand(() => EdgeSnappingEnabled = !EdgeSnappingEnabled);
            ToggleClickThroughCommand = new RelayCommand(() => ClickThroughEnabled = !ClickThroughEnabled);
            ClearGuidesCommand = new RelayCommand(ClearGuides);
            CopyMeasurementCommand = new RelayCommand(CopyMeasurement);
            CycleOpacityCommand = new RelayCommand(CycleOpacity);
        }

        #region Persisted Properties

        /// <summary>Ruler length along its own axis, in pixels.</summary>
        public int Width
        {
            get => _width;
            set => SetPersisted(ref _width,
                RulerDefaults.Clamp(value, RulerDefaults.MinWidth, RulerDefaults.MaxWidth));
        }

        /// <summary>Ruler thickness, in pixels.</summary>
        public int Height
        {
            get => _height;
            set => SetPersisted(ref _height,
                RulerDefaults.Clamp(value, RulerDefaults.MinHeight, RulerDefaults.MaxHeight));
        }

        public int PositionX
        {
            get => _positionX;
            set => SetPersisted(ref _positionX, value);
        }

        public int PositionY
        {
            get => _positionY;
            set => SetPersisted(ref _positionY, value);
        }

        /// <summary>Rotation in degrees, always normalized to 0-359.</summary>
        public int Rotation
        {
            get => _rotation;
            set => SetPersisted(ref _rotation, RulerDefaults.NormalizeRotation(value));
        }

        /// <summary>Background opacity as a percentage.</summary>
        public int Opacity
        {
            get => _opacity;
            set
            {
                var clamped = RulerDefaults.Clamp(value, RulerDefaults.MinOpacity, RulerDefaults.MaxOpacity);
                if (SetPersisted(ref _opacity, clamped))
                    InvalidateBackgroundBrush();
            }
        }

        public string Color
        {
            get => _color;
            set
            {
                if (SetPersisted(ref _color, RulerColors.IsKnown(value) ? value : RulerDefaults.Color))
                    InvalidateBackgroundBrush();
            }
        }

        /// <summary>Active measurement unit.</summary>
        public MeasurementUnit Unit
        {
            get => _unit;
            set
            {
                if (SetPersisted(ref _unit, value))
                    OnPropertyChanged(nameof(UnitKey));
            }
        }

        /// <summary>String form of <see cref="Unit"/>, for menu check-state binding.</summary>
        public string UnitKey => _unit.ToKey();

        /// <summary>Calibrated pixels per inch.</summary>
        public int Ppi
        {
            get => _ppi;
            set
            {
                var clamped = RulerDefaults.Clamp(value, RulerDefaults.MinPpi, RulerDefaults.MaxPpi);
                if (SetPersisted(ref _ppi, clamped))
                    _measurementEngine.Ppi = clamped;
            }
        }

        public int MagnifierZoom
        {
            get => _magnifierZoom;
            set => SetPersisted(ref _magnifierZoom,
                RulerDefaults.Clamp(value, RulerDefaults.MinMagnifierZoom, RulerDefaults.MaxMagnifierZoom));
        }

        public bool MagnifierEnabled
        {
            get => _magnifierEnabled;
            set => SetPersisted(ref _magnifierEnabled, value);
        }

        public bool EdgeSnappingEnabled
        {
            get => _edgeSnappingEnabled;
            set => SetPersisted(ref _edgeSnappingEnabled, value);
        }

        /// <summary>
        /// When set, the ruler ignores the mouse entirely and clicks reach the window
        /// underneath. The tray icon stays visible while this is on, because a
        /// click-through ruler cannot be clicked to switch it back off.
        /// </summary>
        public bool ClickThroughEnabled
        {
            get => _clickThroughEnabled;
            set => SetPersisted(ref _clickThroughEnabled, value);
        }

        #endregion

        #region Computed Properties

        /// <summary>Opacity as the 0.0-1.0 fraction WPF expects.</summary>
        public double OpacityValue => _opacity / 100.0;

        /// <summary>
        /// Physical screen pixels per DIP on the ruler's current monitor. Set by the view,
        /// never persisted - it is a property of the display, not of the ruler.
        ///
        /// The ruler body is drawn in real pixels and scaled down by this factor, so any
        /// on-ruler chrome must be scaled back up by it to keep a constant apparent size.
        /// </summary>
        public double PixelScale
        {
            get => _pixelScale;
            set
            {
                if (value > 0 && SetProperty(ref _pixelScale, value))
                    OnPropertyChanged(nameof(ResizeHandleWidth));
            }
        }

        /// <summary>Resize strip width in ruler pixels, holding a constant apparent size.</summary>
        public double ResizeHandleWidth => RulerDefaults.ResizeHandleWidth * _pixelScale;

        /// <summary>
        /// Background brush for the current colour and opacity.
        /// Cached and frozen; rebuilt only when colour or opacity changes.
        /// </summary>
        public Brush BackgroundBrush =>
            _cachedBackgroundBrush ??= RulerColors.CreateBrush(_color, OpacityValue);

        /// <summary>The ruler's length rendered in the active unit, e.g. "500 px".</summary>
        public string FormattedLength => _measurementEngine.Format(_width, _unit);

        private void InvalidateBackgroundBrush()
        {
            _cachedBackgroundBrush = null;
            OnPropertyChanged(nameof(OpacityValue));
            OnPropertyChanged(nameof(BackgroundBrush));
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Applies the persisted configuration to this ViewModel.
        /// Runs with saving suppressed so restoring state does not immediately rewrite the file.
        /// </summary>
        public void LoadConfiguration()
        {
            var config = _configService.Load();

            _isLoading = true;
            try
            {
                Width = config.Size.Width;
                Height = config.Size.Height;
                PositionX = config.Position.X;
                PositionY = config.Position.Y;
                Rotation = config.Rotation;
                Opacity = config.Opacity;
                Color = config.Color;
                Unit = MeasurementUnits.Parse(config.Unit);
                Ppi = config.Ppi;
                MagnifierZoom = config.MagnifierZoom;
                MagnifierEnabled = config.MagnifierEnabled;
                EdgeSnappingEnabled = config.EdgeSnappingEnabled;
                ClickThroughEnabled = config.ClickThroughEnabled;
            }
            finally
            {
                _isLoading = false;
            }

            InvalidateBackgroundBrush();
            OnPropertyChanged(nameof(FormattedLength));
        }

        /// <summary>
        /// Writes current state to disk immediately, cancelling any pending debounced save.
        /// </summary>
        public void SaveConfiguration()
        {
            _saveTimer.Stop();
            _savePending = false;

            // Shortcuts are re-attached by ConfigurationService so a hand-edited
            // shortcut map is never overwritten by an automatic save.
            _configService.Save(new RulerConfig
            {
                Position = new Position { X = _positionX, Y = _positionY },
                Size = new Models.Size { Width = _width, Height = _height },
                Rotation = _rotation,
                Unit = _unit.ToKey(),
                Opacity = _opacity,
                Color = _color,
                Ppi = _ppi,
                MagnifierZoom = _magnifierZoom,
                MagnifierEnabled = _magnifierEnabled,
                EdgeSnappingEnabled = _edgeSnappingEnabled,
                ClickThroughEnabled = _clickThroughEnabled
            });
        }

        /// <summary>
        /// Sets a backing field, raises change notification, and schedules a debounced save.
        /// </summary>
        private bool SetPersisted<T>(ref T storage, T value,
            [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            if (!SetProperty(ref storage, value, propertyName))
                return false;

            if (propertyName is nameof(Width) or nameof(Unit) or nameof(Ppi))
                OnPropertyChanged(nameof(FormattedLength));

            ScheduleSave();
            return true;
        }

        private void ScheduleSave()
        {
            if (_isLoading || _disposed)
                return;

            _savePending = true;

            // Restarting the timer coalesces a burst of changes into a single write
            // once the user pauses.
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void FlushPendingSave()
        {
            if (_savePending)
                SaveConfiguration();
            else
                _saveTimer.Stop();
        }

        #endregion

        #region Commands

        /// <summary>Sets rotation to a preset angle. Parameter is the angle in degrees.</summary>
        public ICommand SetRotationCommand { get; }

        /// <summary>Restores the default size, rotation and a centered position.</summary>
        public ICommand ResetPositionCommand { get; }

        /// <summary>Sets the measurement unit. Parameter is a unit key such as "inches".</summary>
        public ICommand SetUnitCommand { get; }

        /// <summary>Sets opacity. Parameter is a percentage such as "60".</summary>
        public ICommand SetOpacityCommand { get; }

        /// <summary>Sets the ruler colour. Parameter is a colour name such as "cyan".</summary>
        public ICommand SetColorCommand { get; }

        /// <summary>Steps opacity through the preset levels.</summary>
        public ICommand CycleOpacityCommand { get; }

        public ICommand ToggleMagnifierCommand { get; }

        public ICommand ToggleEdgeSnappingCommand { get; }

        /// <summary>Toggles whether the ruler passes mouse input through to what is below.</summary>
        public ICommand ToggleClickThroughCommand { get; }

        public ICommand ClearGuidesCommand { get; }

        public ICommand CopyMeasurementCommand { get; }

        /// <summary>
        /// Returns the ruler to its default size and rotation, centered on the primary monitor.
        /// </summary>
        public void ResetPosition()
        {
            Rotation = RulerDefaults.Rotation;
            Width = RulerDefaults.Width;
            Height = RulerDefaults.Height;

            var center = ScreenHelper.GetCenteredPosition(Width, Height);
            PositionX = center.X;
            PositionY = center.Y;
        }

        private void ClearGuides() => EdgeGuides.Clear();

        /// <summary>
        /// Steps to the next opacity preset, wrapping around at the end.
        /// </summary>
        private void CycleOpacity()
        {
            var steps = RulerDefaults.OpacitySteps;
            int index = Array.IndexOf(steps, _opacity);
            Opacity = steps[(index < 0 ? 0 : index + 1) % steps.Length];
        }

        private void CopyMeasurement()
        {
            var measurement = FormattedLength;

            if (ClipboardService.CopyToClipboard(measurement))
                MeasurementCopied?.Invoke(this, measurement);
        }

        #endregion

        #region Position Validation

        /// <summary>
        /// Ensures the ruler is reachable on some monitor after a config restore.
        ///
        /// Checks every display rather than only the primary one, so a ruler saved on a
        /// secondary monitor (which can legitimately sit at negative coordinates) is left
        /// where the user put it. Only a ruler that is genuinely off every screen — for
        /// instance because a monitor was unplugged — is recentered.
        /// </summary>
        public void ValidatePosition()
        {
            var bounds = new Drawing.Rectangle(PositionX, PositionY, Math.Max(_width, 1), Math.Max(_height, 1));

            if (ScreenHelper.IsReachable(bounds, RulerDefaults.MinVisiblePixels))
                return;

            System.Diagnostics.Debug.WriteLine(
                $"[RulerViewModel] Saved position {bounds} is off-screen; recentering.");

            ResetPosition();
        }

        #endregion

        /// <summary>
        /// Stops the save timer and writes out any change still waiting to be persisted.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_savePending)
                SaveConfiguration();

            _saveTimer.Stop();
            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}

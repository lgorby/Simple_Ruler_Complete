using CommunityToolkit.Mvvm.Input;
using RulerOverlay.Models;
using RulerOverlay.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace RulerOverlay.ViewModels
{
    /// <summary>
    /// ViewModel for the main ruler window
    /// Manages all ruler state and configuration
    /// </summary>
    public class RulerViewModel : ViewModelBase
    {
        private readonly ConfigurationService _configService;
        private readonly MeasurementEngine _measurementEngine;

        /// <summary>
        /// Event raised when measurement should be copied to clipboard
        /// </summary>
        public event EventHandler<string>? MeasurementCopied;

        private int _width = 500;
        private int _height = 90;
        private int _positionX = 0;
        private int _positionY = 0;
        private int _rotation = 0;
        private int _opacity = 100;
        private string _color = "white";
        private string _unit = "pixels";
        private int _ppi = 96;
        private int _magnifierZoom = 4;
        private bool _magnifierEnabled = false;
        private bool _edgeSnappingEnabled = false;

        public ObservableCollection<EdgeGuide> EdgeGuides { get; } = new ObservableCollection<EdgeGuide>();

        public RulerViewModel(ConfigurationService configService)
        {
            _configService = configService;
            _measurementEngine = new MeasurementEngine(_ppi);

            // Initialize commands
            SetRotationCommand = new RelayCommand<string>(angle =>
            {
                if (int.TryParse(angle, out int value))
                {
                    System.Diagnostics.Debug.WriteLine($"Setting rotation to {value}°");
                    Rotation = value;
                }
            });
            ResetPositionCommand = new RelayCommand(ResetPosition);
            SetUnitCommand = new RelayCommand<string>(unit => Unit = unit ?? "pixels");
            SetOpacityCommand = new RelayCommand<string>(opacity =>
            {
                if (int.TryParse(opacity, out int value))
                {
                    System.Diagnostics.Debug.WriteLine($"Setting opacity to {value}%");
                    Opacity = value;
                }
            });
            SetColorCommand = new RelayCommand<string>(color => Color = color ?? "white");
            ToggleMagnifierCommand = new RelayCommand(() => MagnifierEnabled = !MagnifierEnabled);
            ToggleEdgeSnappingCommand = new RelayCommand(() => EdgeSnappingEnabled = !EdgeSnappingEnabled);
            ClearGuidesCommand = new RelayCommand(ClearGuides);
            CopyMeasurementCommand = new RelayCommand(CopyMeasurement);
        }

        #region Properties

        public int Width
        {
            get => _width;
            set
            {
                if (SetProperty(ref _width, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public int Height
        {
            get => _height;
            set
            {
                if (SetProperty(ref _height, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public int PositionX
        {
            get => _positionX;
            set
            {
                if (SetProperty(ref _positionX, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public int PositionY
        {
            get => _positionY;
            set
            {
                if (SetProperty(ref _positionY, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public int Rotation
        {
            get => _rotation;
            set
            {
                if (SetProperty(ref _rotation, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public int Opacity
        {
            get => _opacity;
            set
            {
                if (SetProperty(ref _opacity, value))
                {
                    OnPropertyChanged(nameof(OpacityValue));
                    AutoSaveConfiguration();
                }
            }
        }

        public string Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(BackgroundBrush));
                    AutoSaveConfiguration();
                }
            }
        }

        public string Unit
        {
            get => _unit;
            set
            {
                if (SetProperty(ref _unit, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public int Ppi
        {
            get => _ppi;
            set
            {
                if (SetProperty(ref _ppi, value))
                {
                    _measurementEngine.SetPPI(value);
                    AutoSaveConfiguration();
                }
            }
        }

        public int MagnifierZoom
        {
            get => _magnifierZoom;
            set
            {
                if (SetProperty(ref _magnifierZoom, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public bool MagnifierEnabled
        {
            get => _magnifierEnabled;
            set
            {
                if (SetProperty(ref _magnifierEnabled, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        public bool EdgeSnappingEnabled
        {
            get => _edgeSnappingEnabled;
            set
            {
                if (SetProperty(ref _edgeSnappingEnabled, value))
                {
                    AutoSaveConfiguration();
                }
            }
        }

        #endregion

        #region Computed Properties for UI Binding

        /// <summary>
        /// Opacity value for WPF (0.0 to 1.0)
        /// </summary>
        public double OpacityValue => _opacity / 100.0;

        /// <summary>
        /// Background brush based on selected color
        /// </summary>
        public Brush BackgroundBrush
        {
            get
            {
                return _color.ToLower() switch
                {
                    "white" => Brushes.White,
                    "black" => Brushes.Black,
                    "yellow" => Brushes.Yellow,
                    "cyan" => Brushes.Cyan,
                    _ => Brushes.White
                };
            }
        }

        #endregion

        #region Configuration Management

        /// <summary>
        /// Loads configuration from file and applies to ViewModel
        /// </summary>
        public void LoadConfiguration()
        {
            var config = _configService.Load();

            // Apply loaded values without triggering auto-save
            _width = config.Size.Width;
            _height = config.Size.Height;
            _positionX = config.Position.X;
            _positionY = config.Position.Y;
            _rotation = config.Rotation;
            _opacity = config.Opacity;
            _color = config.Color;
            _unit = config.Unit;
            _ppi = config.Ppi;
            _magnifierZoom = config.MagnifierZoom;
            _magnifierEnabled = config.MagnifierEnabled;
            _edgeSnappingEnabled = config.EdgeSnappingEnabled;

            // Update measurement engine
            _measurementEngine.SetPPI(_ppi);

            // Notify UI of all property changes
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(PositionX));
            OnPropertyChanged(nameof(PositionY));
            OnPropertyChanged(nameof(Rotation));
            OnPropertyChanged(nameof(Opacity));
            OnPropertyChanged(nameof(Color));
            OnPropertyChanged(nameof(Unit));
            OnPropertyChanged(nameof(Ppi));
            OnPropertyChanged(nameof(MagnifierZoom));
            OnPropertyChanged(nameof(MagnifierEnabled));
            OnPropertyChanged(nameof(EdgeSnappingEnabled));
            OnPropertyChanged(nameof(OpacityValue));
            OnPropertyChanged(nameof(BackgroundBrush));
        }

        /// <summary>
        /// Saves current ViewModel state to configuration file
        /// </summary>
        public void SaveConfiguration()
        {
            var config = new RulerConfig
            {
                Position = new Position { X = _positionX, Y = _positionY },
                Size = new Models.Size { Width = _width, Height = _height },
                Rotation = _rotation,
                Unit = _unit,
                Opacity = _opacity,
                Color = _color,
                Ppi = _ppi,
                MagnifierZoom = _magnifierZoom,
                MagnifierEnabled = _magnifierEnabled,
                EdgeSnappingEnabled = _edgeSnappingEnabled
            };

            _configService.Save(config);
        }

        private void AutoSaveConfiguration()
        {
            // Auto-save on every property change
            SaveConfiguration();
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to set rotation to a specific angle (accepts string parameter)
        /// </summary>
        public ICommand SetRotationCommand { get; }

        /// <summary>
        /// Command to reset position to center screen
        /// </summary>
        public ICommand ResetPositionCommand { get; }

        /// <summary>
        /// Command to set measurement unit (accepts string parameter)
        /// </summary>
        public ICommand SetUnitCommand { get; }

        /// <summary>
        /// Command to set opacity level (accepts string parameter)
        /// </summary>
        public ICommand SetOpacityCommand { get; }

        /// <summary>
        /// Command to set ruler color (accepts string parameter)
        /// </summary>
        public ICommand SetColorCommand { get; }

        /// <summary>
        /// Command to toggle magnifier on/off
        /// </summary>
        public ICommand ToggleMagnifierCommand { get; }

        /// <summary>
        /// Command to toggle edge snapping on/off
        /// </summary>
        public ICommand ToggleEdgeSnappingCommand { get; }

        /// <summary>
        /// Command to clear all edge guides
        /// </summary>
        public ICommand ClearGuidesCommand { get; }

        /// <summary>
        /// Command to copy current measurement to clipboard
        /// </summary>
        public ICommand CopyMeasurementCommand { get; }

        private void ResetPosition()
        {
            // Reset rotation
            Rotation = 0;

            // Reset to default size
            Width = 500;
            Height = 90;

            // Center on screen
            var screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
            var screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

            PositionX = (screenWidth - Width) / 2;
            PositionY = (screenHeight - Height) / 2;
        }

        private void ClearGuides()
        {
            EdgeGuides.Clear();
        }

        private void CopyMeasurement()
        {
            var clipboardService = new ClipboardService();
            var measurement = clipboardService.FormatMeasurement(Width, Unit, Ppi);

            if (clipboardService.CopyToClipboard(measurement))
            {
                // Raise event to show toast notification
                MeasurementCopied?.Invoke(this, measurement);
            }
        }

        #endregion

        #region Position Validation

        /// <summary>
        /// Validates and corrects ruler position to ensure it's visible on screen
        /// At least 100px must be visible
        /// </summary>
        public void ValidatePosition()
        {
            var screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
            var screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

            const int minVisiblePixels = 100;

            // Ensure ruler is not too far off-screen
            if (PositionX + Width < minVisiblePixels)
            {
                // Too far left, bring it back
                PositionX = minVisiblePixels - Width;
            }

            if (PositionX > screenWidth - minVisiblePixels)
            {
                // Too far right, bring it back
                PositionX = screenWidth - minVisiblePixels;
            }

            if (PositionY + Height < minVisiblePixels)
            {
                // Too far up, bring it back
                PositionY = minVisiblePixels - Height;
            }

            if (PositionY > screenHeight - minVisiblePixels)
            {
                // Too far down, bring it back
                PositionY = screenHeight - minVisiblePixels;
            }

            // If still completely off-screen, reset to center
            if (PositionX < -Width + minVisiblePixels ||
                PositionX > screenWidth ||
                PositionY < -Height + minVisiblePixels ||
                PositionY > screenHeight)
            {
                ResetPosition();
            }
        }

        #endregion
    }
}

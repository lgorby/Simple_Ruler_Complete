# Ruler Overlay

A desktop measurement overlay tool that provides precise on-screen measurements for designers, developers, and anyone needing pixel-perfect measurements.

## Features

- **Precise Measurements**: Display measurements in pixels, inches, or centimeters
- **Flexible Ruler**: Move, resize, and rotate the ruler to any angle
- **Point-to-Point Mode**: Click and drag to measure distances between any two points
- **Edge Highlighting**: Create temporary visual guides on the ruler
- **Magnifier**: Zoom in on ruler corners for precise pixel measurements
- **Edge Snapping**: Automatically snap to color boundaries on screen
- **Calibration**: Calibrate to your specific screen for accurate measurements
- **Customization**: Adjust transparency and choose from multiple colors
- **Keyboard Shortcuts**: Quick access to all features
- **Portable**: No installation required - runs as standalone EXE

## Technology Stack

- **Framework**: C# WPF (Windows Presentation Foundation)
- **Runtime**: .NET 8.0
- **UI Pattern**: MVVM (Model-View-ViewModel)
- **Styling**: XAML with data binding
- **Build Tool**: Visual Studio 2022 / .NET CLI

## Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 (recommended) or .NET CLI
- Windows 10/11

## Installation & Setup

### Using Visual Studio 2022

1. Clone this repository
2. Open `RulerOverlay.sln` in Visual Studio 2022
3. Build and run (F5)

### Using .NET CLI

1. Clone this repository
2. Navigate to the project directory
3. Build and run:
   ```bash
   dotnet build
   dotnet run --project RulerOverlay
   ```

## Development

### Project Structure

```
ruler-overlay/
├── RulerOverlay/
│   ├── Windows/               # WPF Windows
│   │   ├── RulerWindow.xaml
│   │   ├── CalibrationDialog.xaml
│   │   ├── PointToPointWindow.xaml
│   │   └── HelpWindow.xaml
│   ├── ViewModels/            # MVVM ViewModels
│   │   ├── RulerViewModel.cs
│   │   ├── CalibrationViewModel.cs
│   │   ├── PointToPointViewModel.cs
│   │   └── ViewModelBase.cs
│   ├── Services/              # Business logic
│   │   ├── MeasurementEngine.cs
│   │   ├── ConfigurationService.cs
│   │   ├── ClipboardService.cs
│   │   ├── EdgeSnappingService.cs
│   │   ├── GlobalHotkeyService.cs
│   │   └── ScreenCaptureService.cs
│   ├── Models/                # Data models
│   │   ├── RulerConfig.cs
│   │   ├── MeasurementUnit.cs
│   │   └── EdgeGuide.cs
│   ├── Controls/              # Custom WPF controls
│   │   ├── MagnifierControl.xaml
│   │   └── ToastControl.xaml
│   ├── Converters/            # WPF value converters
│   │   └── MenuConverters.cs
│   ├── Helpers/               # Utility classes
│   │   ├── DpiHelper.cs
│   │   └── Win32Helper.cs
│   ├── Utils/                 # Rendering utilities
│   │   └── RulerRenderer.cs
│   ├── icon/                  # Application icon
│   │   ├── icon.ico
│   │   └── icon.png
│   ├── App.xaml               # Application entry point
│   └── RulerOverlay.csproj    # Project file
├── RulerOverlay.sln           # Visual Studio solution
└── .gitignore
```

### Building Release Version

Using Visual Studio:
1. Set configuration to "Release"
2. Build > Build Solution
3. The executable will be in `bin/Release/net8.0-windows/`

Using .NET CLI:
```bash
dotnet publish -c Release
```

The executable can be run from any location without installation.

## Configuration

All settings are stored in a JSON config file located in the Windows AppData folder:
```
%APPDATA%/ruler-overlay/config.json
```

Settings include:
- Ruler position, size, and rotation
- Current measurement unit
- Transparency and color preferences
- PPI calibration value
- Magnifier zoom level
- Keyboard shortcut preferences

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+R` | Reset ruler to center |
| `Ctrl+C` | Copy measurement to clipboard |
| `Ctrl+T` | Toggle transparency |
| `Ctrl+M` | Toggle magnifier |
| `Ctrl+S` | Toggle edge snapping |
| `Ctrl+P` | Enter point-to-point mode |
| `Ctrl+Q` | Quit application |
| `Esc` | Exit point-to-point mode |
| `F1` | Show help dialog |

## Usage

1. **Move the Ruler**: Click and drag anywhere on the ruler body
2. **Resize**: Drag from edges or corners
3. **Rotate**: Right-click for preset angles or hold Ctrl and drag outside the ruler for free rotation
4. **Change Units**: Right-click and select from pixels, inches, or centimeters
5. **Calibrate**: Right-click > Calibration > Enter your screen diagonal in inches
6. **Point-to-Point**: Press Ctrl+P or right-click > Point-to-Point Mode, then click and drag to measure

## License

MIT License - feel free to use and modify for your needs.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

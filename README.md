# Ruler Overlay

A desktop measurement overlay tool that provides precise on-screen measurements for designers, developers, and anyone needing pixel-perfect measurements.

## Features

- **Precise Measurements**: Display measurements in real screen pixels, inches, or centimeters, correct on high-DPI and scaled displays
- **Flexible Ruler**: Move, resize from any edge or corner, and rotate freely or to preset angles
- **Point-to-Point Mode**: Click and drag to measure distances between any two points
- **Edge Highlighting**: Create temporary visual guides on the ruler
- **Magnifier**: Zoom in near any ruler edge for precise pixel placement
- **Edge Snapping**: Automatically snap to color boundaries on screen
- **Calibration**: Calibrate to your specific screen for accurate measurements
- **Click-Through Mode**: Let clicks reach the window underneath while the ruler stays visible on top
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
│   │   └── ScreenCaptureService.cs
│   ├── Models/                # Data models and shared constants
│   │   ├── RulerConfig.cs
│   │   ├── RulerDefaults.cs   # single source of truth for defaults/limits
│   │   ├── RulerColors.cs
│   │   ├── MeasurementUnit.cs
│   │   └── EdgeGuide.cs
│   ├── Controls/              # Custom WPF controls
│   │   ├── MagnifierControl.xaml
│   │   └── ToastControl.xaml
│   ├── Converters/            # WPF value converters
│   │   └── MenuConverters.cs
│   ├── Helpers/               # Utility classes
│   │   ├── CursorFactory.cs   # draws the rotation cursor WPF lacks
│   │   ├── DpiHelper.cs
│   │   ├── ScreenHelper.cs    # DPI + multi-monitor geometry
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
dotnet publish RulerOverlay/RulerOverlay.csproj -c Release -o publish
```

Or just run `build.bat`, which does the same thing and drops the result in
`publish/RulerOverlay.exe`.

The result is a self-contained single-file EXE (~155 MB) that runs from any
location with no .NET runtime installed. Build output (`bin/`, `obj/`,
`publish/`) is deliberately not committed, so a fresh clone must be built
before there is an EXE to run.

## How Measurements Work

The ruler measures in **physical screen pixels**, not the scaled ("logical" or
device-independent) pixels that WPF, CSS and most design tools use. On a display
running at 125% scaling, a ruler reading `500 px` covers 500 actual device pixels
— it will look shorter than a 500-unit measurement in a tool that reports logical
pixels, and that is the intended behaviour for a screen ruler.

This also matters for inches and centimetres: calibration derives PPI from your
monitor's true pixel resolution, so it is only consistent if the length it is
applied to is measured in those same real pixels.

The ruler re-detects scaling when dragged between monitors, so it stays accurate
on a mixed-DPI multi-monitor setup.

## Configuration

All settings are stored in a JSON config file located in the Windows AppData folder:
```
%APPDATA%\RulerOverlay\config.json
```

This lives outside the repository, so it does not travel with a clone: on a new
machine the ruler starts at its defaults, centered at 500x90. Copy that file
across if you want your position, size, calibration and colour to follow you.
The file is written atomically and validated on load, so a hand-edited or
partially written config falls back to sane values rather than breaking startup.

Settings include:
- Ruler position, size, and rotation
- Current measurement unit
- Transparency and color preferences
- PPI calibration value
- Magnifier zoom level
- Edge snapping and click-through toggles
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
| `Ctrl+G` | Clear all edge guides |
| `Ctrl+K` | Toggle click-through mode |
| `Ctrl+Q` | Quit application |
| `Arrows` | Nudge ruler 1px (hold `Shift` for 10px) |
| `Esc` | Exit point-to-point mode |
| `F1` | Show help dialog |

Shortcuts are active while the ruler window has focus. They are deliberately not
registered as system-wide hotkeys, which would take over `Ctrl+C` and friends in
every other application.

## Usage

1. **Move the Ruler**: Click and drag anywhere on the ruler body, or nudge it with the arrow keys
2. **Resize**: Drag any edge to change one dimension, or a corner to change both. Left/right edges set the length, top/bottom set the thickness
3. **Rotate**: Drag from just outside any **corner** — the standard rotation gesture, no modifier key needed. A curved arrow cursor and a small arc appear there on hover. Holding **Shift** snaps to 15° steps, and the live angle is shown while you drag. **Ctrl+drag** anywhere on the body does the same thing. For a fixed angle, right-click > Rotation (0°, 45°, 90°, 135°, 180°) or use the ⤾ button
4. **Change Units**: Right-click and select from pixels, inches, or centimeters
5. **Calibrate**: Right-click > Calibrate Screen, then enter your screen diagonal in inches
6. **Point-to-Point**: Press Ctrl+P or right-click > Point-to-Point Mode, then click and drag to measure
7. **Edge Guides**: Shift+click on the ruler to drop a magenta guide; Shift+click a guide to remove it
8. **Edge Snapping**: Press Ctrl+S, then drag a ruler end to within ~8 px of a colour boundary and it snaps onto it. Works while the ruler is horizontal, and snaps to the *visible* window edge rather than the invisible resize border Windows adds
9. **Click-Through**: Press **Ctrl+K** to let clicks pass straight through to the window underneath while the ruler stays visible on top. The tray icon is shown while this is on, because a click-through ruler cannot be clicked to switch it back off
10. **Magnifier**: Press Ctrl+M, then move the cursor near a ruler edge. The caption reads `cursor position / ruler length`. In point-to-point mode it is on by default (press M to toggle) and shows the live distance

## Closing the App

The ruler is an overlay with no title bar, so it closes only through its own exit
points: the **✕** button, **Ctrl+Q**, or **Exit** on the system-tray icon. External
close requests such as Alt+F4 are ignored, so a stray keystroke cannot dismiss the
ruler mid-measurement. A Windows sign-out or shutdown still closes it normally.

## Project State

Implemented and verified: DPI-correct measurement in real screen pixels,
calibration, pixels/inches/centimeters, preset rotation, edge guides, edge
snapping, magnifier (ruler and point-to-point), clipboard copy, system tray.

Free rotation, resize from every edge and corner, and click-through mode are
implemented and tested.

Rotation follows the usual convention from drawing tools: grab just outside a
corner, no modifier required. Ctrl+drag on the body is kept as an alternative.

### Known trade-offs

- The four rotation zones sit just outside the ruler's corners, so the window is
  padded by about 18 px on every side and those four small squares absorb clicks
  that would otherwise reach the window behind. This is the cost of a standard
  rotation handle; `RulerDefaults.RotationZoneSize` is the dial.
- Transparency applies to the ruler body only. Markings stay fully opaque so the
  scale is always readable, which means a very transparent ruler still shows a
  solid scale rather than fading out entirely.

`CLAUDE.md` holds the original project specification and is the backlog. It
describes the intended feature set rather than the current state, so treat any
difference between it and this section as work outstanding.

### Development notes

- `.gitattributes` pins line endings, so a clone on another machine will not
  show every file as modified.
- Shortcuts are window-local by design, never system-wide hotkeys — registering
  `Ctrl+C` globally would hijack it in every other application.
- Ruler geometry is in physical screen pixels; WPF chrome is in DIPs. The
  `ScaleTransform` on `RootGrid` is what bridges the two, and
  `Helpers/ScreenHelper.cs` owns every conversion. Mixing the two spaces has
  caused three separate bugs in this codebase, so convert at the boundary.
- The window is layered (`AllowsTransparency="True"`), so **Windows hit-tests it
  by per-pixel alpha**. Anything painted with a fully transparent brush is a hole:
  the click passes to the window underneath and never reaches WPF. Interactive
  elements that sit outside the ruler's opaque background must therefore paint a
  non-zero alpha — see the `HitTestableBrush` used by the rotation zones. Elements
  layered over the ruler body, like the resize handles, are unaffected.
- Rotation and resize both hold one point fixed while the ruler changes shape, via
  `LocalToScreen` in `RulerWindow.xaml.cs`. Changing the angle or size alters the
  window's bounding box, so the position has to be corrected each frame or the
  ruler drifts away from the cursor.

## License

MIT License - feel free to use and modify for your needs.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

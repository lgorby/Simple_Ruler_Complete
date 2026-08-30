using RulerOverlay.Helpers;
using RulerOverlay.Models;
using RulerOverlay.Services;
using RulerOverlay.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;
using Cursors = System.Windows.Input.Cursors;
using Cursor = System.Windows.Input.Cursor;
using Key = System.Windows.Input.Key;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Mouse = System.Windows.Input.Mouse;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;

namespace RulerOverlay.Windows
{
    /// <summary>
    /// The always-on-top ruler overlay.
    /// </summary>
    public partial class RulerWindow : Window
    {
        private readonly RulerViewModel _viewModel;
        private readonly EdgeSnappingService _edgeSnapping;

        /// <summary>True when no config file existed at startup, so nothing is worth restoring.</summary>
        private readonly bool _isFirstRun;

        /// <summary>
        /// Physical screen pixels per device-independent pixel for the monitor the ruler is on.
        ///
        /// The ruler's Width and Height are counts of real screen pixels, because that is what
        /// the app reports and what the PPI calibration is derived from. WPF lays out in DIPs,
        /// so this factor converts between the two and is the sole reason the ruler measures
        /// correctly on a scaled display.
        /// </summary>
        private double _pixelScale = 1.0;

        private WinForms.NotifyIcon? _notifyIcon;
        private WinForms.ContextMenuStrip? _trayMenu;
        private Drawing.Icon? _trayIcon;

        // Resize state
        private bool _isResizing;
        private bool _isLeftEdge;
        private bool _suppressWindowSizeUpdate;
        private bool _resizeExpanded;
        private Point _resizeStartScreenPoint;
        private double _initialWidth;
        private double _anchorX;
        private double _anchorY;
        private double _lastResizeLeft;
        private double _lastResizeTop;

        private bool _isClosing;

        /// <summary>
        /// Set only by <see cref="RequestClose"/>. Any close that did not come from one of
        /// the app's own exit affordances is refused, so the overlay cannot be dismissed by
        /// a stray Alt+F4 while the user is measuring.
        /// </summary>
        private bool _closeRequested;

        /// <summary>Windows is logging off or shutting down; a close must not be refused.</summary>
        private bool _sessionEnding;

        /// <summary>The modal point-to-point overlay, while it is open.</summary>
        private PointToPointWindow? _pointToPointWindow;

        public RulerWindow()
        {
            InitializeComponent();

            var configService = new ConfigurationService();
            _viewModel = new RulerViewModel(configService);
            _isFirstRun = configService.IsFirstRun;
            _edgeSnapping = new EdgeSnappingService();
            DataContext = _viewModel;

            Loaded += RulerWindow_Loaded;
            Closing += RulerWindow_Closing;
            Closed += RulerWindow_Closed;
            MouseMove += RulerWindow_MouseMove;
            MouseLeave += RulerWindow_MouseLeave;
            SizeChanged += RulerWindow_SizeChanged;
            StateChanged += RulerWindow_StateChanged;

            _viewModel.MeasurementCopied += ViewModel_MeasurementCopied;
            Toast.Dismissed += (_, _) => ToastPopup.IsOpen = false;

            if (Application.Current != null)
                Application.Current.SessionEnding += Application_SessionEnding;
        }

        #region Lifecycle

        private void RulerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.LoadConfiguration();

            if (_isFirstRun)
            {
                // Nothing was saved, so open centred on screen at the default size
                // rather than in the top-left corner.
                _viewModel.ResetPosition();
            }
            else
            {
                // Drop the ruler back onto a monitor if the saved position is no longer reachable.
                _viewModel.ValidatePosition();
            }

            ApplyPixelScale();

            Left = ScreenHelper.ToLogical(_viewModel.PositionX, _pixelScale);
            Top = ScreenHelper.ToLogical(_viewModel.PositionY, _pixelScale);

            Magnifier.ZoomLevel = _viewModel.MagnifierZoom;

            AdjustWindowSizeForRotation(_viewModel.Rotation);

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            RenderMarkings();
            InitializeSystemTray();
        }

        private void Application_SessionEnding(object? sender, SessionEndingCancelEventArgs e) =>
            _sessionEnding = true;

        /// <summary>
        /// The single funnel for intentionally quitting: the close button, Ctrl+Q and the
        /// tray Exit item all route through here.
        /// </summary>
        private void RequestClose()
        {
            _closeRequested = true;

            // A modal child would otherwise keep its own message loop running and block
            // the owner from closing.
            if (_pointToPointWindow != null)
            {
                _pointToPointWindow.Close();
                _pointToPointWindow = null;
            }

            Close();
        }

        private void RulerWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_closeRequested && !_sessionEnding)
            {
                // Not one of the app's own exit paths, so leave the ruler where it is.
                e.Cancel = true;
                return;
            }

            _isClosing = true;

            // Flush any change still sitting in the debounce window.
            _viewModel.SaveConfiguration();
        }

        private void RulerWindow_Closed(object? sender, EventArgs e)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.MeasurementCopied -= ViewModel_MeasurementCopied;
            _viewModel.Dispose();

            if (Application.Current != null)
                Application.Current.SessionEnding -= Application_SessionEnding;

            // Popups are separate top-level windows; leaving one open can keep the
            // process alive after the ruler itself has gone.
            HideMagnifier();
            ToastPopup.IsOpen = false;

            DisposeSystemTray();

            // ShutdownMode alone would normally suffice, but being explicit guarantees
            // the process ends even if some other window is still referenced.
            Application.Current?.Shutdown();
        }

        /// <summary>
        /// Re-applies the pixel scale when the ruler is dragged onto a monitor with
        /// different scaling, so it keeps measuring real pixels there too.
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);

            ApplyPixelScale();
            AdjustWindowSizeForRotation(_viewModel.Rotation);
            RenderMarkings();
        }

        /// <summary>
        /// Sets the transform that makes one RootGrid unit equal one physical screen pixel.
        /// Windows reports a single uniform scaling factor per monitor, so the horizontal
        /// value is used for both axes to keep rotation isotropic.
        /// </summary>
        private void ApplyPixelScale()
        {
            _pixelScale = ScreenHelper.GetDpiScale(this).X;

            PixelScaleTransform.ScaleX = 1.0 / _pixelScale;
            PixelScaleTransform.ScaleY = 1.0 / _pixelScale;

            // Lets the on-ruler chrome cancel the scale out and stay a usable size.
            _viewModel.PixelScale = _pixelScale;

            // The magnifier draws its reticle in real device pixels.
            Magnifier.PixelScale = _pixelScale;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // WS_EX_TOOLWINDOW keeps the overlay out of Alt+Tab; WS_EX_LAYERED enables
            // the per-pixel transparency the ruler background relies on.
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
                return;

            var extendedStyle = Win32Helper.GetWindowLong(hwnd, Win32Helper.GWL_EXSTYLE);
            Win32Helper.SetWindowLong(hwnd, Win32Helper.GWL_EXSTYLE,
                extendedStyle | Win32Helper.WS_EX_LAYERED | Win32Helper.WS_EX_TOOLWINDOW);
        }

        #endregion

        #region Rendering

        private void RulerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // During a resize the drag handler drives redraws through the ViewModel.
            if (!_isResizing)
                RenderMarkings();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(RulerViewModel.Unit):
                case nameof(RulerViewModel.Ppi):
                // The markings are drawn in a shade that contrasts with the ruler colour,
                // so they have to be repainted when that colour changes.
                case nameof(RulerViewModel.Color):
                    RenderMarkings();
                    break;

                case nameof(RulerViewModel.Width):
                case nameof(RulerViewModel.Height):
                case nameof(RulerViewModel.Rotation):
                    RenderMarkings();
                    AdjustWindowSizeForRotation(_viewModel.Rotation);
                    break;

                case nameof(RulerViewModel.MagnifierZoom):
                    Magnifier.ZoomLevel = _viewModel.MagnifierZoom;
                    break;

                case nameof(RulerViewModel.MagnifierEnabled):
                    if (!_viewModel.MagnifierEnabled)
                        HideMagnifier();
                    break;
            }
        }

        /// <summary>
        /// Redraws the tick marks for the ruler's own dimensions. The rotation transform
        /// on RootGrid turns the whole canvas, so markings are always drawn unrotated.
        /// </summary>
        private void RenderMarkings()
        {
            Utils.RulerRenderer.DrawMarkings(
                MarkingsCanvas,
                _viewModel.Width,
                _viewModel.Height,
                _viewModel.Unit,
                _viewModel.Ppi,
                _viewModel.Rotation,
                _pixelScale,
                _viewModel.Color);
        }

        /// <summary>
        /// Sizes the window to the axis-aligned bounding box of the rotated ruler and
        /// shifts the content so it sits inside that box.
        /// </summary>
        private void AdjustWindowSizeForRotation(int rotation)
        {
            // The resize handler owns window size and transforms while a drag is active.
            if (_suppressWindowSizeUpdate)
                return;

            // Corner positions are computed in ruler pixels, then divided by the pixel
            // scale because Window.Width/Height and TranslateTransform are in DIPs.
            double rulerWidth = _viewModel.Width;
            double rulerHeight = _viewModel.Height;
            var (cos, sin) = GetRotationVector(rotation);

            // The four ruler corners after rotating about (0,0).
            double x2 = rulerWidth * cos;
            double y2 = rulerWidth * sin;
            double x3 = -rulerHeight * sin;
            double y3 = rulerHeight * cos;
            double x4 = x2 + x3;
            double y4 = y2 + y3;

            double minX = Math.Min(0, Math.Min(x2, Math.Min(x3, x4)));
            double maxX = Math.Max(0, Math.Max(x2, Math.Max(x3, x4)));
            double minY = Math.Min(0, Math.Min(y2, Math.Min(y3, y4)));
            double maxY = Math.Max(0, Math.Max(y2, Math.Max(y3, y4)));

            Width = Math.Ceiling(ScreenHelper.ToLogical(maxX - minX, _pixelScale));
            Height = Math.Ceiling(ScreenHelper.ToLogical(maxY - minY, _pixelScale));

            RotationTransform.CenterX = 0;
            RotationTransform.CenterY = 0;

            // Shift the rotated content back into positive window space.
            TranslationTransform.X = ScreenHelper.ToLogical(-minX, _pixelScale);
            TranslationTransform.Y = ScreenHelper.ToLogical(-minY, _pixelScale);

            RootGrid.Margin = new Thickness(0);

            UpdateResizeCursors(rotation);
        }

        /// <summary>
        /// Cosine and sine of a rotation in degrees. Every place that needs to project
        /// along the ruler's axis goes through this rather than repeating the conversion.
        /// </summary>
        private static (double Cos, double Sin) GetRotationVector(int degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            return (Math.Cos(radians), Math.Sin(radians));
        }

        /// <summary>
        /// Points the resize handles' cursors along the ruler's current axis, snapped to
        /// the nearest 45° sector.
        /// </summary>
        private void UpdateResizeCursors(int rotation)
        {
            // Eight 45° sectors, offset by 22.5° so each angle maps to its nearest axis.
            int sector = (int)Math.Round(RulerDefaults.NormalizeRotation(rotation) / 45.0) % 8;

            Cursor cursor = (sector % 4) switch
            {
                0 => Cursors.SizeWE,    // 0° / 180°
                1 => Cursors.SizeNESW,  // 45° / 225°
                2 => Cursors.SizeNS,    // 90° / 270°
                _ => Cursors.SizeNWSE   // 135° / 315°
            };

            LeftResizeHandle.Cursor = cursor;
            RightResizeHandle.Cursor = cursor;
        }

        #endregion

        #region System Tray

        private void InitializeSystemTray()
        {
            try
            {
                _trayMenu = new WinForms.ContextMenuStrip();
                _trayMenu.Items.Add("Show Ruler", null, (_, _) => RestoreFromTray());
                _trayMenu.Items.Add("Exit", null, (_, _) => RequestClose());

                _notifyIcon = new WinForms.NotifyIcon
                {
                    Text = "Ruler Overlay",
                    ContextMenuStrip = _trayMenu,
                    Visible = false
                };

                _trayIcon = LoadTrayIcon();
                _notifyIcon.Icon = _trayIcon;

                _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RulerWindow] Tray icon setup failed: {ex.Message}");
                DisposeSystemTray();
            }
        }

        /// <summary>
        /// Loads the executable's own icon, falling back to a stock icon.
        /// Without an icon a NotifyIcon is invisible, which would strand a
        /// minimized ruler with no way to bring it back.
        /// </summary>
        private static Drawing.Icon LoadTrayIcon()
        {
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var icon = Drawing.Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                        return icon;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RulerWindow] Could not load app icon: {ex.Message}");
            }

            return Drawing.SystemIcons.Application;
        }

        private void MinimizeToTray()
        {
            HideMagnifier();

            if (_notifyIcon == null)
            {
                // Without a tray icon there is nothing to click to get the ruler back, and
                // the window has no taskbar button either, so a plain minimize would strand
                // it. Give it a taskbar button for the trip.
                ShowInTaskbar = true;
                WindowState = WindowState.Minimized;
                return;
            }

            _notifyIcon.Visible = true;
            Hide();
        }

        private void RestoreFromTray()
        {
            if (_notifyIcon != null)
                _notifyIcon.Visible = false;

            Show();
            WindowState = WindowState.Normal;

            // Back to overlay behaviour: no taskbar presence while visible.
            ShowInTaskbar = false;
            Activate();
        }

        /// <summary>
        /// Restores overlay behaviour if the window was un-minimized from the taskbar
        /// rather than through the tray icon.
        /// </summary>
        private void RulerWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Normal && ShowInTaskbar)
                ShowInTaskbar = false;
        }

        private void DisposeSystemTray()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            _trayMenu?.Dispose();
            _trayMenu = null;

            // SystemIcons members are shared and must not be disposed.
            if (_trayIcon != null && _trayIcon != Drawing.SystemIcons.Application)
                _trayIcon.Dispose();
            _trayIcon = null;
        }

        #endregion

        #region Toolbar Buttons

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e) => MinimizeToTray();

        /// <summary>
        /// Advances to the next preset angle. Uses the same list the Rotation menu shows,
        /// so the button can never land on an angle with no matching menu entry.
        /// </summary>
        private void QuickRotate_Click(object sender, RoutedEventArgs e)
        {
            var presets = RulerDefaults.RotationPresets;
            int index = Array.IndexOf(presets, _viewModel.Rotation);
            _viewModel.Rotation = presets[(index + 1) % presets.Length];
        }

        private void ShowContextMenu_Click(object sender, RoutedEventArgs e)
        {
            var menu = RulerBackground.ContextMenu;
            if (menu == null)
                return;

            // Anchor under the ruler, since this came from a button rather than a click point.
            menu.PlacementTarget = RulerBackground;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        /// <summary>
        /// Restores cursor-anchored placement before a right-click opens the menu, undoing
        /// the button-anchored placement set by <see cref="ShowContextMenu_Click"/>.
        /// </summary>
        private void RulerBackground_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (RulerBackground.ContextMenu is { } menu)
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        }

        private void CloseApp_Click(object sender, RoutedEventArgs e) => RequestClose();

        #endregion

        #region Keyboard

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Shortcuts are window-local by design. Registering them system-wide would
            // hijack Ctrl+C and friends in every other application.
            var modifiers = Keyboard.Modifiers;
            bool ctrl = modifiers.HasFlag(ModifierKeys.Control);
            bool shift = modifiers.HasFlag(ModifierKeys.Shift);

            if (ctrl && HandleControlShortcut(e.Key))
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F1)
            {
                ShowHelpWindow();
                e.Handled = true;
                return;
            }

            if (TryNudge(e.Key, shift ? RulerDefaults.NudgeStepLarge : RulerDefaults.NudgeStep))
                e.Handled = true;
        }

        /// <summary>
        /// Runs the Ctrl+key shortcut for a key, if there is one.
        /// </summary>
        /// <returns>True when the key was handled.</returns>
        private bool HandleControlShortcut(Key key)
        {
            switch (key)
            {
                case Key.R:
                    _viewModel.ResetPositionCommand.Execute(null);
                    SyncWindowToViewModelPosition();
                    return true;

                case Key.C:
                    _viewModel.CopyMeasurementCommand.Execute(null);
                    return true;

                case Key.T:
                    _viewModel.CycleOpacityCommand.Execute(null);
                    return true;

                case Key.M:
                    _viewModel.ToggleMagnifierCommand.Execute(null);
                    return true;

                case Key.S:
                    _viewModel.ToggleEdgeSnappingCommand.Execute(null);
                    return true;

                case Key.P:
                    EnterPointToPointMode();
                    return true;

                case Key.G:
                    _viewModel.ClearGuidesCommand.Execute(null);
                    return true;

                case Key.Q:
                    RequestClose();
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Moves the ruler by a keyboard step.
        /// </summary>
        /// <returns>True when the key was an arrow key.</returns>
        private bool TryNudge(Key key, int step)
        {
            double deltaX = 0;
            double deltaY = 0;

            switch (key)
            {
                case Key.Left: deltaX = -step; break;
                case Key.Right: deltaX = step; break;
                case Key.Up: deltaY = -step; break;
                case Key.Down: deltaY = step; break;
                default: return false;
            }

            // The step is a count of real pixels, matching what the ruler measures.
            Left += ScreenHelper.ToLogical(deltaX, _pixelScale);
            Top += ScreenHelper.ToLogical(deltaY, _pixelScale);
            StoreWindowPosition();
            return true;
        }

        #endregion

        #region Position

        /// <summary>
        /// Records the window's current position in the ViewModel, converting from WPF's
        /// device-independent units to the physical pixels the config file stores.
        /// </summary>
        private void StoreWindowPosition()
        {
            _viewModel.PositionX = (int)Math.Round(ScreenHelper.ToPhysical(Left, _pixelScale));
            _viewModel.PositionY = (int)Math.Round(ScreenHelper.ToPhysical(Top, _pixelScale));
        }

        /// <summary>
        /// Moves the window to the position currently held by the ViewModel.
        /// </summary>
        private void SyncWindowToViewModelPosition()
        {
            Left = ScreenHelper.ToLogical(_viewModel.PositionX, _pixelScale);
            Top = ScreenHelper.ToLogical(_viewModel.PositionY, _pixelScale);
        }

        #endregion

        #region Drag to Move

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _isResizing)
                return;

            // Clicks that land on a toolbar button belong to that button.
            if (IsWithinButton(e.OriginalSource as DependencyObject))
                return;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                ToggleEdgeGuideAt(e.GetPosition(RootGrid));
                return;
            }

            try
            {
                DragMove();
                StoreWindowPosition();
            }
            catch (InvalidOperationException ex)
            {
                // DragMove throws if the button was already released, or during another drag.
                System.Diagnostics.Debug.WriteLine($"[RulerWindow] DragMove ignored: {ex.Message}");
            }
        }

        /// <summary>
        /// Walks up the visual tree looking for a Button ancestor.
        /// </summary>
        private bool IsWithinButton(DependencyObject? source)
        {
            while (source != null && source != this)
            {
                if (source is Button)
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        /// <summary>
        /// Adds a guide at the clicked position, or removes the one already there.
        /// </summary>
        private void ToggleEdgeGuideAt(Point position)
        {
            // Resolve the target before mutating, rather than removing mid-enumeration.
            EdgeGuide? existing = null;
            foreach (var guide in _viewModel.EdgeGuides)
            {
                if (Math.Abs(guide.Position - position.X) <= RulerDefaults.GuideHitTolerance)
                {
                    existing = guide;
                    break;
                }
            }

            if (existing != null)
            {
                _viewModel.EdgeGuides.Remove(existing);
                return;
            }

            if (position.X < 0 || position.X > _viewModel.Width)
                return;

            var label = new MeasurementEngine(_viewModel.Ppi).Format(position.X, _viewModel.Unit);
            _viewModel.EdgeGuides.Add(new EdgeGuide(position.X, label));
        }

        #endregion

        #region Edge Resize

        /// <summary>
        /// Begins a resize by expanding the window to fill the screen.
        ///
        /// With the window already covering the screen, the drag can be expressed entirely
        /// as WPF transforms. Calling SetWindowPos on every mouse move instead makes DWM
        /// briefly present the previous frame at the new position, which reads as a jump.
        /// </summary>
        private void BeginResize(bool isLeftEdge, MouseButtonEventArgs e, UIElement handle)
        {
            _isResizing = true;
            _isLeftEdge = isLeftEdge;
            _suppressWindowSizeUpdate = true;
            _resizeExpanded = false;
            _resizeStartScreenPoint = PointToScreen(e.GetPosition(this));
            _initialWidth = _viewModel.Width;

            // The ruler edge that must stay put while the opposite edge follows the mouse.
            // Left/Top and TranslationTransform are DIPs, so the ruler length is converted.
            var (cos, sin) = GetRotationVector(_viewModel.Rotation);
            double lengthDip = ScreenHelper.ToLogical(_viewModel.Width, _pixelScale);

            if (isLeftEdge)
            {
                _anchorX = Left + lengthDip * cos + TranslationTransform.X;
                _anchorY = Top + lengthDip * sin + TranslationTransform.Y;
            }
            else
            {
                _anchorX = Left + TranslationTransform.X;
                _anchorY = Top + TranslationTransform.Y;
            }

            // Work out the expansion while the window is still at its original position.
            var origin = PointToScreen(new Point(0, 0));
            var screenBounds = ScreenHelper.GetScreenBoundsFromPhysicalPoint(origin.X, origin.Y);

            double scrLeft = ScreenHelper.ToLogical(screenBounds.Left, _pixelScale);
            double scrTop = ScreenHelper.ToLogical(screenBounds.Top, _pixelScale);

            _lastResizeLeft = scrLeft;
            _lastResizeTop = scrTop;

            double newTransX = TranslationTransform.X + (Left - scrLeft);
            double newTransY = TranslationTransform.Y + (Top - scrTop);

            // Phase 1: hide the content so WPF submits a transparent frame to DWM.
            RootGrid.Opacity = 0;

            handle.CaptureMouse();
            e.Handled = true;

            // Phase 2: once that transparent frame is up, grow the window. DWM now has
            // nothing stale to show at the new position.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (!_isResizing)
                {
                    // The button came back up before expansion; just restore the content.
                    RootGrid.Opacity = 1;
                    return;
                }

                TranslationTransform.X = newTransX;
                TranslationTransform.Y = newTransY;

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    Win32Helper.SetWindowPos(hwnd, IntPtr.Zero,
                        screenBounds.Left, screenBounds.Top,
                        screenBounds.Width, screenBounds.Height,
                        Win32Helper.SWP_NOZORDER | Win32Helper.SWP_NOACTIVATE);
                }

                RootGrid.Opacity = 1;
                _resizeExpanded = true;
            });
        }

        private void LeftResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                BeginResize(isLeftEdge: true, e, (UIElement)sender);
        }

        private void RightResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                BeginResize(isLeftEdge: false, e, (UIElement)sender);
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing || !_resizeExpanded || e.LeftButton != MouseButtonState.Pressed)
                return;

            // Screen coordinates, so a change in window position cannot skew the delta.
            // These are physical pixels, which is exactly the unit Width is measured in,
            // so the drag distance needs no conversion.
            var currentScreenPoint = PointToScreen(e.GetPosition(this));

            double deltaX = currentScreenPoint.X - _resizeStartScreenPoint.X;
            double deltaY = currentScreenPoint.Y - _resizeStartScreenPoint.Y;

            var (cos, sin) = GetRotationVector(_viewModel.Rotation);

            // Project the mouse movement onto the ruler's own axis.
            double delta = deltaX * cos + deltaY * sin;
            double newWidth = _isLeftEdge ? _initialWidth - delta : _initialWidth + delta;

            newWidth = TrySnapWidth(newWidth, currentScreenPoint, cos);

            int newRulerWidth = (int)Math.Max(RulerDefaults.MinWidth, newWidth);
            double newLengthDip = ScreenHelper.ToLogical(newRulerWidth, _pixelScale);

            // Keep the anchored edge fixed inside the expanded window.
            if (_isLeftEdge)
            {
                // Anchor is the far end: anchorX = expandedLeft + length + transX
                TranslationTransform.X = _anchorX - _lastResizeLeft - newLengthDip * cos;
                TranslationTransform.Y = _anchorY - _lastResizeTop - newLengthDip * sin;
            }
            else
            {
                // Anchor is the near end: anchorX = expandedLeft + transX
                TranslationTransform.X = _anchorX - _lastResizeLeft;
                TranslationTransform.Y = _anchorY - _lastResizeTop;
            }

            // The Width setter raises PropertyChanged, which redraws the markings.
            _viewModel.Width = newRulerWidth;

            e.Handled = true;
        }

        /// <summary>Height of the strip scanned for colour boundaries, in physical pixels.</summary>
        private const int SnapScanBandHeight = 24;

        /// <summary>Gap between the ruler body and the scanned strip, in physical pixels.</summary>
        private const int SnapScanBandGap = 2;

        /// <summary>
        /// Nudges the dragged width so the ruler's moving edge lands on a colour boundary
        /// in the content being measured.
        ///
        /// The correction is measured against the ruler's own edge, not the mouse cursor.
        /// The grab handle is several pixels wide, so the cursor generally sits a little
        /// inside the edge; snapping the cursor instead would leave the ruler overhanging
        /// the target by exactly that offset.
        ///
        /// The strip that gets scanned sits just outside the ruler body rather than under
        /// it. During a resize the window is expanded to cover the screen and the ruler is
        /// drawn opaque, so scanning under it would only ever rediscover the ruler's own
        /// edge. The element being measured almost always extends past the ruler, so a
        /// strip immediately below or above it sees the real boundary; both are tried
        /// because either one can fall outside the target.
        ///
        /// Only applied while the ruler is axis-aligned: the detector looks for vertical
        /// edges, which is only meaningful when the ruler's own edge is vertical.
        /// </summary>
        private double TrySnapWidth(double proposedWidth, Point cursorScreenPoint, double cos)
        {
            if (!_viewModel.EdgeSnappingEnabled)
                return proposedWidth;

            int rotation = RulerDefaults.NormalizeRotation(_viewModel.Rotation);
            if (rotation != 0 && rotation != 180)
                return proposedWidth;

            // Where the dragged edge currently sits, in physical screen pixels. The anchor
            // is the opposite (stationary) end, held in DIP screen coordinates.
            double anchorScreenX = ScreenHelper.ToPhysical(_anchorX, _pixelScale);
            double edgeScreenX = _isLeftEdge
                ? anchorScreenX - proposedWidth * cos
                : anchorScreenX + proposedWidth * cos;

            var snappedX = FindEdgeNear(edgeScreenX, cursorScreenPoint);
            if (snappedX == null)
                return proposedWidth;

            double correction = (snappedX.Value - edgeScreenX) * cos;
            return _isLeftEdge ? proposedWidth - correction : proposedWidth + correction;
        }

        /// <summary>
        /// Looks for a colour boundary near <paramref name="edgeScreenX"/> in the strips
        /// immediately below and above the ruler, returning whichever candidate is closest.
        /// </summary>
        private double? FindEdgeNear(double edgeScreenX, Point cursorScreenPoint)
        {
            // Screen extent of the ruler body, so the scan can avoid it.
            var rulerTopLeft = RootGrid.PointToScreen(new Point(0, 0));
            var rulerBottomLeft = RootGrid.PointToScreen(new Point(0, _viewModel.Height));
            double rulerTop = Math.Min(rulerTopLeft.Y, rulerBottomLeft.Y);
            double rulerBottom = Math.Max(rulerTopLeft.Y, rulerBottomLeft.Y);

            var screen = ScreenHelper.GetScreenBoundsFromPhysicalPoint(cursorScreenPoint.X, cursorScreenPoint.Y);

            double belowTop = rulerBottom + SnapScanBandGap;
            double aboveTop = rulerTop - SnapScanBandGap - SnapScanBandHeight;

            double? best = null;
            double bestDistance = double.MaxValue;

            foreach (var bandTop in new[] { belowTop, aboveTop })
            {
                if (bandTop < screen.Top || bandTop + SnapScanBandHeight > screen.Bottom)
                    continue;

                var candidate = _edgeSnapping.FindNearestVerticalEdge(
                    edgeScreenX, bandTop, SnapScanBandHeight);

                if (candidate == null)
                    continue;

                double distance = Math.Abs(candidate.Value - edgeScreenX);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private void ResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing)
                return;

            _isResizing = false;
            _suppressWindowSizeUpdate = false;
            ((UIElement)sender).ReleaseMouseCapture();

            // Shrink back to the exact bounding box for the final ruler size.
            AdjustWindowSizeForRotation(_viewModel.Rotation);

            // Place the window so the anchored edge ends up where it started.
            var (cos, sin) = GetRotationVector(_viewModel.Rotation);
            double lengthDip = ScreenHelper.ToLogical(_viewModel.Width, _pixelScale);

            if (_isLeftEdge)
            {
                Left = _anchorX - (lengthDip * cos + TranslationTransform.X);
                Top = _anchorY - (lengthDip * sin + TranslationTransform.Y);
            }
            else
            {
                Left = _anchorX - TranslationTransform.X;
                Top = _anchorY - TranslationTransform.Y;
            }

            RootGrid.Opacity = 1;
            StoreWindowPosition();

            e.Handled = true;
        }

        #endregion

        #region Calibration

        private void CalibrateScreen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CalibrationDialog(_viewModel.Ppi) { Owner = this };

            if (dialog.ShowDialog() != true)
                return;

            _viewModel.Ppi = dialog.CalibratedPpi;

            // A toast is less disruptive than a modal box for a confirmation.
            ShowToast($"Calibrated to {_viewModel.Ppi} PPI");
        }

        #endregion

        #region Toast

        private void ViewModel_MeasurementCopied(object? sender, string measurement) =>
            ShowToast($"Copied: {measurement}");

        /// <summary>
        /// Shows a transient message just below the ruler.
        /// </summary>
        private void ShowToast(string message)
        {
            if (_isClosing)
                return;

            var dpi = ScreenHelper.GetDpiScale(this);
            var origin = PointToScreen(new Point(0, 0));
            var work = ScreenHelper.GetWorkingAreaFromPhysicalPoint(origin.X, origin.Y);

            // Popup offsets for Placement="Absolute" are device-independent units,
            // so the physical working area has to be converted before use.
            double workLeft = ScreenHelper.ToLogical(work.Left, dpi.X);
            double workTop = ScreenHelper.ToLogical(work.Top, dpi.Y);
            double workWidth = ScreenHelper.ToLogical(work.Width, dpi.X);
            double workBottom = ScreenHelper.ToLogical(work.Bottom, dpi.Y);

            const double toastWidth = 240;
            double desiredX = Left + (ActualWidth - toastWidth) / 2;
            double desiredY = Top + ActualHeight + 8;

            ToastPopup.HorizontalOffset = Math.Clamp(desiredX, workLeft, workLeft + workWidth - toastWidth);
            ToastPopup.VerticalOffset = Math.Min(desiredY, workBottom - 60);
            ToastPopup.IsOpen = true;

            Toast.ShowMessage(message);
        }

        #endregion

        #region Magnifier

        private void RulerWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_viewModel.MagnifierEnabled || _isResizing)
            {
                HideMagnifier();
                return;
            }

            // Measured against RootGrid so the ruler's rotation is already accounted for.
            var position = e.GetPosition(RootGrid);

            double rulerWidth = _viewModel.Width;
            double rulerHeight = _viewModel.Height;
            const double threshold = RulerDefaults.MagnifierEdgeThreshold;

            bool insideRuler = position.X >= 0 && position.X <= rulerWidth &&
                               position.Y >= 0 && position.Y <= rulerHeight;

            bool nearEdge = insideRuler && (
                position.X <= threshold ||
                position.X >= rulerWidth - threshold ||
                position.Y <= threshold ||
                position.Y >= rulerHeight - threshold);

            if (!nearEdge)
            {
                HideMagnifier();
                return;
            }

            ShowMagnifier();
            Magnifier.UpdatePosition(position.X, _viewModel.Width, _viewModel.Unit, _viewModel.Ppi);
        }

        private void RulerWindow_MouseLeave(object sender, MouseEventArgs e) => HideMagnifier();

        private void ShowMagnifier()
        {
            if (!MagnifierPopup.IsOpen)
            {
                MagnifierPopup.IsOpen = true;
                Magnifier.Start();
            }

            UpdateMagnifierPosition();
        }

        /// <summary>
        /// Parks the magnifier in a corner of the current monitor, away from the cursor.
        /// </summary>
        private void UpdateMagnifierPosition()
        {
            var cursorScreen = PointToScreen(Mouse.GetPosition(this));

            var (x, y) = ScreenHelper.GetOverlayCornerPosition(
                cursorScreen.X, cursorScreen.Y,
                RulerDefaults.MagnifierSize, RulerDefaults.MagnifierMargin, _pixelScale);

            MagnifierPopup.HorizontalOffset = x;
            MagnifierPopup.VerticalOffset = y;
        }

        private void HideMagnifier()
        {
            if (!MagnifierPopup.IsOpen)
                return;

            MagnifierPopup.IsOpen = false;
            Magnifier.Stop();
        }

        #endregion

        #region Point-to-Point Mode

        private void PointToPointMode_Click(object sender, RoutedEventArgs e) => EnterPointToPointMode();

        private void EnterPointToPointMode()
        {
            HideMagnifier();

            var viewModel = new PointToPointViewModel(
                new MeasurementEngine(_viewModel.Ppi),
                _viewModel.Unit);

            var window = new PointToPointWindow(viewModel, _viewModel.MagnifierZoom) { Owner = this };
            _pointToPointWindow = window;

            // The ruler would otherwise sit on top of the measurement overlay.
            Visibility = Visibility.Hidden;
            try
            {
                window.ShowDialog();
            }
            finally
            {
                _pointToPointWindow = null;

                // Restore even if the dialog throws, so the ruler cannot be left invisible,
                // unless the app is on its way out.
                if (!_closeRequested)
                    Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Help

        private void ShowHelp_Click(object sender, RoutedEventArgs e) => ShowHelpWindow();

        private void ShowHelpWindow()
        {
            new HelpWindow { Owner = this }.ShowDialog();
        }

        #endregion
    }
}

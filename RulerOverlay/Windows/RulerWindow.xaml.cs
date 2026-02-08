using RulerOverlay.Helpers;
using RulerOverlay.Services;
using RulerOverlay.ViewModels;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WinForms = System.Windows.Forms; // Alias for Windows Forms
using Drawing = System.Drawing; // Alias for System.Drawing
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButton = System.Windows.Input.MouseButton;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using Mouse = System.Windows.Input.Mouse;


namespace RulerOverlay.Windows
{
    public partial class RulerWindow : Window
    {
        private readonly ConfigurationService _configService;
        private readonly RulerViewModel _viewModel;
        private readonly Services.EdgeSnappingService _edgeSnapping;
        private WinForms.NotifyIcon? _notifyIcon;

        private bool _isResizing = false;
        private bool _isLeftEdge = false;
        private bool _suppressWindowSizeUpdate = false;
        private Point _resizeStartScreenPoint;
        private double _initialWidth;
        private double _anchorX;
        private double _anchorY;
        private double _lastResizeLeft;
        private double _lastResizeTop;
        private bool _resizeExpanded = false;

        public RulerWindow()
        {
            InitializeComponent();

            _configService = new ConfigurationService();
            _viewModel = new RulerViewModel(_configService);
            _edgeSnapping = new Services.EdgeSnappingService();
            DataContext = _viewModel;

            // Load configuration and restore window state
            Loaded += RulerWindow_Loaded;
            Closing += RulerWindow_Closing;
            MouseMove += RulerWindow_MouseMove;
            MouseLeave += RulerWindow_MouseLeave;
            SizeChanged += RulerWindow_SizeChanged;

            // Subscribe to measurement copied event
            _viewModel.MeasurementCopied += ViewModel_MeasurementCopied;
        }

        private void RulerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Skip during resize - the resize handler triggers RenderMarkings via ViewModel
            if (!_isResizing)
                RenderMarkings();
        }

        private void ViewModel_MeasurementCopied(object? sender, string measurement)
        {
            Toast.Show($"Copied: {measurement}");
        }

        private void RulerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Restore window position and size from config
            _viewModel.LoadConfiguration();

            // Validate position to ensure ruler is visible
            _viewModel.ValidatePosition();

            Left = _viewModel.PositionX;
            Top = _viewModel.PositionY;
            // Width and Height are managed by SizeToContent based on RootGrid dimensions

            // Adjust window size for saved rotation
            AdjustWindowSizeForRotation(_viewModel.Rotation);

            // Subscribe to property changes to redraw markings
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Initial render
            RenderMarkings();

            // Initialize system tray icon
            InitializeSystemTray();
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Redraw markings when unit, PPI, width, height, or rotation changes
            if (e.PropertyName == nameof(_viewModel.Unit) ||
                e.PropertyName == nameof(_viewModel.Ppi) ||
                e.PropertyName == nameof(_viewModel.Width) ||
                e.PropertyName == nameof(_viewModel.Height) ||
                e.PropertyName == nameof(_viewModel.Rotation))
            {
                RenderMarkings();
            }

            // Adjust window size when rotation, width, or height changes
            if (e.PropertyName == nameof(_viewModel.Rotation) ||
                e.PropertyName == nameof(_viewModel.Width) ||
                e.PropertyName == nameof(_viewModel.Height))
            {
                AdjustWindowSizeForRotation(_viewModel.Rotation);
            }
        }

        private void RenderMarkings()
        {
            // Always use the ruler's original dimensions (not the window's dimensions)
            // The LayoutTransform will rotate the canvas with the markings
            // Pass rotation so labels can counter-rotate to stay horizontal
            Utils.RulerRenderer.DrawMarkings(
                MarkingsCanvas,
                _viewModel.Width,
                _viewModel.Height,
                _viewModel.Unit,
                _viewModel.Ppi,
                _viewModel.Rotation
            );
        }

        private void AdjustWindowSizeForRotation(int rotation)
        {
            // During resize, the resize handler manages window size and transforms directly.
            // Skip this entirely to avoid conflicting with the resize handler's state.
            if (_suppressWindowSizeUpdate)
                return;

            double rulerWidth = _viewModel.Width;
            double rulerHeight = _viewModel.Height;
            double angleRad = rotation * Math.PI / 180.0;
            double cos = Math.Cos(angleRad);
            double sin = Math.Sin(angleRad);

            // Calculate where all four corners of the ruler end up after rotation around (0,0)
            // Original corners: (0,0), (W,0), (0,H), (W,H)
            // After rotation by angle θ:
            // (0,0) -> (0,0)
            // (W,0) -> (W*cos, W*sin)
            // (0,H) -> (-H*sin, H*cos)
            // (W,H) -> (W*cos - H*sin, W*sin + H*cos)

            double x1 = 0;
            double y1 = 0;
            double x2 = rulerWidth * cos;
            double y2 = rulerWidth * sin;
            double x3 = -rulerHeight * sin;
            double y3 = rulerHeight * cos;
            double x4 = rulerWidth * cos - rulerHeight * sin;
            double y4 = rulerWidth * sin + rulerHeight * cos;

            // Find bounding box
            double minX = Math.Min(Math.Min(x1, x2), Math.Min(x3, x4));
            double maxX = Math.Max(Math.Max(x1, x2), Math.Max(x3, x4));
            double minY = Math.Min(Math.Min(y1, y2), Math.Min(y3, y4));
            double maxY = Math.Max(Math.Max(y1, y2), Math.Max(y3, y4));

            double boundingWidth = maxX - minX;
            double boundingHeight = maxY - minY;

            // Set window size to bounding box
            this.Width = Math.Ceiling(boundingWidth);
            this.Height = Math.Ceiling(boundingHeight);

            // Set rotation center to (0,0)
            RotationTransform.CenterX = 0;
            RotationTransform.CenterY = 0;

            // Apply translation to shift rotated content into positive window space
            TranslationTransform.X = -minX;
            TranslationTransform.Y = -minY;

            // Clear any margins
            RootGrid.Margin = new Thickness(0);

            // Update resize handle cursors based on rotation
            UpdateResizeCursors(rotation);

            System.Diagnostics.Debug.WriteLine($"[AdjustWindowSizeForRotation] Rotation={rotation}°, Ruler={rulerWidth}x{rulerHeight}, Window={boundingWidth:F1}x{boundingHeight:F1}, Translation=({-minX:F1},{-minY:F1})");
        }

        private void UpdateResizeCursors(int rotation)
        {
            // Normalize rotation to 0-359 range
            int normalizedRotation = ((rotation % 360) + 360) % 360;

            // Determine cursor based on rotation angle
            System.Windows.Input.Cursor cursor;

            if (normalizedRotation >= 337.5 || normalizedRotation < 22.5)
            {
                // 0° (horizontal) - use horizontal resize cursor
                cursor = System.Windows.Input.Cursors.SizeWE;
            }
            else if (normalizedRotation >= 22.5 && normalizedRotation < 67.5)
            {
                // 45° (diagonal NE-SW)
                cursor = System.Windows.Input.Cursors.SizeNESW;
            }
            else if (normalizedRotation >= 67.5 && normalizedRotation < 112.5)
            {
                // 90° (vertical)
                cursor = System.Windows.Input.Cursors.SizeNS;
            }
            else if (normalizedRotation >= 112.5 && normalizedRotation < 157.5)
            {
                // 135° (diagonal NW-SE)
                cursor = System.Windows.Input.Cursors.SizeNWSE;
            }
            else if (normalizedRotation >= 157.5 && normalizedRotation < 202.5)
            {
                // 180° (horizontal)
                cursor = System.Windows.Input.Cursors.SizeWE;
            }
            else if (normalizedRotation >= 202.5 && normalizedRotation < 247.5)
            {
                // 225° (diagonal NE-SW)
                cursor = System.Windows.Input.Cursors.SizeNESW;
            }
            else if (normalizedRotation >= 247.5 && normalizedRotation < 292.5)
            {
                // 270° (vertical)
                cursor = System.Windows.Input.Cursors.SizeNS;
            }
            else // 292.5 to 337.5
            {
                // 315° (diagonal NW-SE)
                cursor = System.Windows.Input.Cursors.SizeNWSE;
            }

            // Apply cursor to both resize handles
            LeftResizeHandle.Cursor = cursor;
            RightResizeHandle.Cursor = cursor;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Set layered window attributes for true transparency
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = Win32Helper.GetWindowLong(hwnd, Win32Helper.GWL_EXSTYLE);
            Win32Helper.SetWindowLong(hwnd, Win32Helper.GWL_EXSTYLE,
                extendedStyle | Win32Helper.WS_EX_LAYERED | Win32Helper.WS_EX_TOOLWINDOW);

            // Keyboard shortcuts are handled locally via Window_PreviewKeyDown
        }


        private void RulerWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save configuration before closing
            _viewModel.SaveConfiguration();

            // Dispose system tray icon
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void InitializeSystemTray()
        {
            // Create a simple icon for the system tray
            _notifyIcon = new WinForms.NotifyIcon
            {
                Text = "Ruler Overlay",
                Visible = false // Hidden by default, shown on minimize
            };

            // Create a simple icon (cyan square representing the ruler)
            using (var bitmap = new Drawing.Bitmap(16, 16))
            using (var g = Drawing.Graphics.FromImage(bitmap))
            {
                g.Clear(Drawing.Color.Cyan);
                _notifyIcon.Icon = Drawing.Icon.FromHandle(bitmap.GetHicon());
            }

            // Double-click to restore window
            _notifyIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void MinimizeToTray()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
                Hide();
            }
        }

        private void RestoreFromTray()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[MinimizeToTray_Click] BUTTON CLICKED!");
            MinimizeToTray();
        }

        private void QuickRotate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[QuickRotate_Click] BUTTON CLICKED!");
            // Cycle through rotation angles: 0 → 90 → 180 → 270 → 0
            int newRotation = (_viewModel.Rotation + 90) % 360;
            _viewModel.Rotation = newRotation;
        }

        private void ShowContextMenu_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[ShowContextMenu_Click] BUTTON CLICKED!");
            // Show the context menu programmatically
            RulerBackground.ContextMenu.IsOpen = true;
        }

        #region Arrow Key Nudge

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            // Handle Ctrl+ shortcuts (local to this window only)
            if (ctrl)
            {
                switch (e.Key)
                {
                    case Key.R:
                        _viewModel.ResetPositionCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.C:
                        _viewModel.CopyMeasurementCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.T:
                        int newOpacity = _viewModel.Opacity switch
                        {
                            100 => 80,
                            80 => 60,
                            60 => 40,
                            40 => 20,
                            _ => 100
                        };
                        _viewModel.Opacity = newOpacity;
                        e.Handled = true;
                        return;
                    case Key.M:
                        _viewModel.ToggleMagnifierCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.S:
                        _viewModel.ToggleEdgeSnappingCommand.Execute(null);
                        e.Handled = true;
                        return;
                    case Key.P:
                        PointToPointMode_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        return;
                    case Key.Q:
                        Close();
                        e.Handled = true;
                        return;
                    case Key.G:
                        _viewModel.ClearGuidesCommand.Execute(null);
                        e.Handled = true;
                        return;
                }
            }

            // F1 - Help (no modifier)
            if (e.Key == Key.F1)
            {
                ShowHelpWindow();
                e.Handled = true;
                return;
            }

            // Arrow key nudge
            int step = shift ? 10 : 1;

            switch (e.Key)
            {
                case Key.Left:
                    Left -= step;
                    break;
                case Key.Right:
                    Left += step;
                    break;
                case Key.Up:
                    Top -= step;
                    break;
                case Key.Down:
                    Top += step;
                    break;
                default:
                    return;
            }

            _viewModel.PositionX = (int)Left;
            _viewModel.PositionY = (int)Top;
            e.Handled = true;
        }

        #endregion

        #region Drag to Move

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] Button={e.ChangedButton}, IsResizing={_isResizing}");
            System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] OriginalSource Type={e.OriginalSource?.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] Source Type={e.Source?.GetType().Name}");

            if (e.ChangedButton == MouseButton.Left && !_isResizing)
            {
                // Check if click is on or inside a button by walking up the visual tree
                var source = e.OriginalSource as DependencyObject;
                int depth = 0;
                while (source != null && source != this)
                {
                    System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] Visual tree depth {depth}: {source.GetType().Name}");

                    if (source is System.Windows.Controls.Button button)
                    {
                        // Let button handle the click
                        System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] Found button in tree, returning to let button handle click");
                        return;
                    }
                    source = System.Windows.Media.VisualTreeHelper.GetParent(source);
                    depth++;
                }

                System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] No button found in visual tree, proceeding with drag");

                // Check if Shift is pressed to create or remove edge guide
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    var position = e.GetPosition(RootGrid);

                    // Check if clicking near an existing guide (within 6px) to remove it
                    Models.EdgeGuide? guideToRemove = null;
                    foreach (var existing in _viewModel.EdgeGuides)
                    {
                        if (Math.Abs(existing.Position - position.X) <= 6)
                        {
                            guideToRemove = existing;
                            break;
                        }
                    }

                    if (guideToRemove != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] Shift pressed near existing guide at {guideToRemove.Position}, removing it");
                        _viewModel.EdgeGuides.Remove(guideToRemove);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] Shift pressed, creating edge guide");
                        var guide = new Models.EdgeGuide(position.X, $"{position.X:F0}px");
                        _viewModel.EdgeGuides.Add(guide);
                    }
                    return;
                }

                // Allow dragging the window
                System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] Starting drag");
                try
                {
                    DragMove();

                    // Update position in ViewModel after drag
                    _viewModel.PositionX = (int)Left;
                    _viewModel.PositionY = (int)Top;
                }
                catch (Exception ex)
                {
                    // DragMove can throw if window is already being dragged
                    System.Diagnostics.Debug.WriteLine($"[Window_MouseDown] DragMove exception: {ex.Message}");
                }
            }
        }

        #endregion

        #region Edge Resize

        /// <summary>
        /// Begins a resize operation by expanding the window to cover the screen.
        /// This allows the resize to happen entirely through WPF transforms (no SetWindowPos
        /// during drag), eliminating the visual jump caused by DWM showing stale buffer content
        /// at a new window position.
        /// </summary>
        private void BeginResize(bool isLeftEdge, MouseButtonEventArgs e, UIElement sender)
        {
            _isResizing = true;
            _isLeftEdge = isLeftEdge;
            _suppressWindowSizeUpdate = true;
            _resizeExpanded = false;
            _resizeStartScreenPoint = PointToScreen(e.GetPosition(this));
            _initialWidth = _viewModel.Width;

            // Compute anchor point (the ruler edge that should stay fixed)
            double angleRad = _viewModel.Rotation * Math.PI / 180.0;
            if (isLeftEdge)
            {
                _anchorX = Left + _viewModel.Width * Math.Cos(angleRad) + TranslationTransform.X;
                _anchorY = Top + _viewModel.Width * Math.Sin(angleRad) + TranslationTransform.Y;
            }
            else
            {
                _anchorX = Left + TranslationTransform.X;
                _anchorY = Top + TranslationTransform.Y;
            }

            // Compute expansion parameters now (while window is at original position)
            var screenPoint = PointToScreen(new Point(0, 0));
            var screen = WinForms.Screen.FromPoint(
                new Drawing.Point((int)screenPoint.X, (int)screenPoint.Y));

            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            double dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

            double scrLeft = screen.Bounds.Left / dpiScaleX;
            double scrTop = screen.Bounds.Top / dpiScaleY;
            double scrWidth = screen.Bounds.Width / dpiScaleX;
            double scrHeight = screen.Bounds.Height / dpiScaleY;

            _lastResizeLeft = scrLeft;
            _lastResizeTop = scrTop;

            double newTransX = TranslationTransform.X + (Left - scrLeft);
            double newTransY = TranslationTransform.Y + (Top - scrTop);

            // Phase 1: Make content invisible. WPF will render a transparent bitmap
            // on the next render pass and submit it to DWM.
            RootGrid.Opacity = 0;

            sender.CaptureMouse();
            e.Handled = true;

            System.Diagnostics.Debug.WriteLine($"[BeginResize] Phase1: Edge={(_isLeftEdge ? "LEFT" : "RIGHT")}, Anchor=({_anchorX:F1},{_anchorY:F1}), Opacity=0 set");

            // Phase 2: After WPF renders the transparent frame, expand the window.
            // DWM now has a transparent buffer, so SetWindowPos won't show a stale ruler
            // at the wrong position.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                if (!_isResizing)
                {
                    // User released mouse before expansion — just restore opacity
                    RootGrid.Opacity = 1;
                    return;
                }

                TranslationTransform.X = newTransX;
                TranslationTransform.Y = newTransY;

                var hwnd = new WindowInteropHelper(this).Handle;
                const uint SWP_NOZORDER = 0x0004;
                const uint SWP_NOACTIVATE = 0x0010;
                Win32Helper.SetWindowPos(hwnd, IntPtr.Zero,
                    (int)(scrLeft * dpiScaleX),
                    (int)(scrTop * dpiScaleY),
                    (int)(scrWidth * dpiScaleX),
                    (int)(scrHeight * dpiScaleY),
                    SWP_NOZORDER | SWP_NOACTIVATE);

                RootGrid.Opacity = 1;
                _resizeExpanded = true;

                System.Diagnostics.Debug.WriteLine($"[BeginResize] Phase2: Expanded to ({scrLeft:F0},{scrTop:F0},{scrWidth:F0}x{scrHeight:F0}), NewTrans=({newTransX:F1},{newTransY:F1}), Opacity=1 restored");
            });
        }

        private void LeftResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                BeginResize(true, e, (UIElement)sender);
        }

        private void RightResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                BeginResize(false, e, (UIElement)sender);
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isResizing && _resizeExpanded && e.LeftButton == MouseButtonState.Pressed)
            {
                // Use screen coordinates so window position changes don't affect the delta
                var currentScreenPoint = PointToScreen(e.GetPosition(this));

                // Calculate delta in screen coordinates
                var screenDeltaX = currentScreenPoint.X - _resizeStartScreenPoint.X;
                var screenDeltaY = currentScreenPoint.Y - _resizeStartScreenPoint.Y;

                // Convert screen (physical) delta to logical (WPF) units for DPI correctness
                var source = PresentationSource.FromVisual(this);
                double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiScaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                var deltaX = screenDeltaX / dpiScaleX;
                var deltaY = screenDeltaY / dpiScaleY;

                // Transform delta based on rotation angle to get delta along ruler axis
                double angleRad = _viewModel.Rotation * Math.PI / 180.0;
                double cos = Math.Cos(angleRad);
                double sin = Math.Sin(angleRad);

                // Project the mouse movement onto the ruler's X axis (width direction)
                double delta = deltaX * cos + deltaY * sin;

                // Compute new ruler width
                int newRulerWidth = (int)Math.Max(100, _isLeftEdge ? _initialWidth - delta : _initialWidth + delta);

                // Compute TranslationTransform so the anchor stays fixed within the expanded window.
                // The expanded window's Left/Top are stored in _lastResizeLeft/_lastResizeTop.
                double newTransX, newTransY;
                if (_isLeftEdge)
                {
                    // Anchor = END of ruler. End in window coords = (W*cos + transX, W*sin + transY)
                    // anchorX = expandedLeft + W*cos + transX  =>  transX = anchorX - expandedLeft - W*cos
                    newTransX = _anchorX - _lastResizeLeft - newRulerWidth * cos;
                    newTransY = _anchorY - _lastResizeTop - newRulerWidth * sin;
                }
                else
                {
                    // Anchor = START of ruler. Start in window coords = (transX, transY)
                    // anchorX = expandedLeft + transX  =>  transX = anchorX - expandedLeft
                    newTransX = _anchorX - _lastResizeLeft;
                    newTransY = _anchorY - _lastResizeTop;
                }

                System.Diagnostics.Debug.WriteLine($"[Resize_Move] Edge={(_isLeftEdge ? "LEFT" : "RIGHT")}, NewWidth={newRulerWidth}, Trans=({newTransX:F1},{newTransY:F1}), Anchor=({_anchorX:F1},{_anchorY:F1})");

                // Update TranslationTransform and ViewModel width.
                // NO SetWindowPos here! The window stays at screen size.
                // All visual changes happen through WPF transforms, which render atomically.
                TranslationTransform.X = newTransX;
                TranslationTransform.Y = newTransY;
                _viewModel.Width = newRulerWidth;

                e.Handled = true;
            }
        }

        private void ResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing)
            {
                _isResizing = false;
                _suppressWindowSizeUpdate = false;
                ((UIElement)sender).ReleaseMouseCapture();

                // Shrink window back to the exact bounding box for the final ruler size.
                // AdjustWindowSizeForRotation computes bounding box, sets Width/Height and TranslationTransform.
                AdjustWindowSizeForRotation(_viewModel.Rotation);

                // Compute final window position from anchor
                double angleRad = _viewModel.Rotation * Math.PI / 180.0;
                double cos = Math.Cos(angleRad);
                double sin = Math.Sin(angleRad);
                double finalLeft, finalTop;
                if (_isLeftEdge)
                {
                    double endWindowX = _viewModel.Width * cos + TranslationTransform.X;
                    double endWindowY = _viewModel.Width * sin + TranslationTransform.Y;
                    finalLeft = _anchorX - endWindowX;
                    finalTop = _anchorY - endWindowY;
                }
                else
                {
                    finalLeft = _anchorX - TranslationTransform.X;
                    finalTop = _anchorY - TranslationTransform.Y;
                }

                Left = finalLeft;
                Top = finalTop;
                _viewModel.PositionX = (int)finalLeft;
                _viewModel.PositionY = (int)finalTop;

                System.Diagnostics.Debug.WriteLine($"[Resize_Up] FinalWidth={_viewModel.Width}, FinalPos=({finalLeft:F1},{finalTop:F1})");

                e.Handled = true;
            }
        }

        #endregion

        #region Calibration

        private void CalibrateScreen_Click(object sender, RoutedEventArgs e)
        {
            var calibrationDialog = new CalibrationDialog(_viewModel.Ppi)
            {
                Owner = this
            };

            if (calibrationDialog.ShowDialog() == true)
            {
                _viewModel.Ppi = calibrationDialog.CalibratedPpi;
                MessageBox.Show($"Screen calibrated! New PPI: {calibrationDialog.CalibratedPpi}",
                    "Calibration Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        #endregion

        #region Magnifier

        private void RulerWindow_MouseMove(object sender, MouseEventArgs e)
        {
            // Only show magnifier if enabled
            if (!_viewModel.MagnifierEnabled)
            {
                HideMagnifier();
                return;
            }

            // Use position relative to the ruler grid so rotation is accounted for
            var position = e.GetPosition(RootGrid);
            const double edgeThreshold = 15.0;

            double rulerWidth = _viewModel.Width;
            double rulerHeight = _viewModel.Height;

            // Check if mouse is within the ruler bounds
            bool insideRuler = position.X >= 0 && position.X <= rulerWidth &&
                               position.Y >= 0 && position.Y <= rulerHeight;

            // Check if near any edge of the ruler
            bool nearEdge = insideRuler && (
                position.X <= edgeThreshold ||
                position.X >= rulerWidth - edgeThreshold ||
                position.Y <= edgeThreshold ||
                position.Y >= rulerHeight - edgeThreshold);

            if (nearEdge)
            {
                ShowMagnifier();
                Magnifier.UpdatePosition(position.X, _viewModel.Unit, _viewModel.Ppi);
            }
            else
            {
                HideMagnifier();
            }
        }

        private void RulerWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            HideMagnifier();
        }

        private void ShowMagnifier()
        {
            if (!MagnifierPopup.IsOpen)
            {
                // Pass the ruler's rotation so the magnifier image matches
                Magnifier.Rotation = _viewModel.Rotation;

                MagnifierPopup.IsOpen = true;
                Magnifier.Start();
            }

            // Reposition every time the mouse moves so it stays out of the way
            UpdateMagnifierPosition();
        }

        private void UpdateMagnifierPosition()
        {
            const double magnifierSize = 200;
            const double margin = 10;

            // Get the monitor the ruler is currently on
            var mouseScreenPos = PointToScreen(Mouse.GetPosition(this));
            var screen = WinForms.Screen.FromPoint(
                new Drawing.Point((int)mouseScreenPos.X, (int)mouseScreenPos.Y));
            var work = screen.WorkingArea;

            // Default: bottom-right corner of the current monitor
            double targetX = work.Right - magnifierSize - margin;
            double targetY = work.Bottom - magnifierSize - margin;

            // If the mouse is near the bottom-right quadrant, move magnifier to bottom-left
            double midX = work.Left + work.Width / 2.0;
            double midY = work.Top + work.Height / 2.0;
            bool mouseInRightHalf = mouseScreenPos.X > midX;
            bool mouseInBottomHalf = mouseScreenPos.Y > midY;

            if (mouseInRightHalf && mouseInBottomHalf)
            {
                targetX = work.Left + margin;
            }
            else if (!mouseInRightHalf && mouseInBottomHalf)
            {
                targetX = work.Right - magnifierSize - margin;
            }

            MagnifierPopup.HorizontalOffset = targetX;
            MagnifierPopup.VerticalOffset = targetY;
        }

        private void HideMagnifier()
        {
            if (MagnifierPopup.IsOpen)
            {
                MagnifierPopup.IsOpen = false;
                Magnifier.Stop();
            }
        }

        #endregion

        #region Point-to-Point Mode

        private void PointToPointMode_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[PointToPointMode_Click] BUTTON CLICKED!");

            // Create point-to-point window
            var viewModel = new PointToPointViewModel(
                new Services.MeasurementEngine(_viewModel.Ppi),
                _viewModel.Unit,
                _viewModel.Ppi
            );

            var pointToPointWindow = new PointToPointWindow(viewModel)
            {
                Owner = this
            };

            // Hide ruler while in point-to-point mode
            this.Visibility = Visibility.Hidden;

            pointToPointWindow.ShowDialog();

            // Show ruler again after point-to-point mode exits
            this.Visibility = Visibility.Visible;
        }

        #endregion

        #region Help

        private void ShowHelp_Click(object sender, RoutedEventArgs e)
        {
            ShowHelpWindow();
        }

        private void ShowHelpWindow()
        {
            var helpWindow = new HelpWindow
            {
                Owner = this
            };
            helpWindow.ShowDialog();
        }

        #endregion

        #region Close

        private void CloseApp_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[CloseApp_Click] BUTTON CLICKED!");
            Close();
        }

        #endregion
    }
}

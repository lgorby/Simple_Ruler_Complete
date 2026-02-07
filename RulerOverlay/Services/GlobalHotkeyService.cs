using RulerOverlay.Helpers;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace RulerOverlay.Services
{
    /// <summary>
    /// Service for registering and handling global keyboard shortcuts
    /// Uses Win32 RegisterHotKey API for system-wide hotkeys
    /// </summary>
    public class GlobalHotkeyService
    {
        private readonly Window _window;
        private readonly Dictionary<int, Action> _hotkeyActions = new();
        private HwndSource? _hwndSource;
        private int _nextHotkeyId = 1;

        public GlobalHotkeyService(Window window)
        {
            _window = window;
        }

        /// <summary>
        /// Initializes the hotkey service and hooks window messages
        /// </summary>
        public void Initialize()
        {
            // Get window handle and hook into message loop
            var helper = new WindowInteropHelper(_window);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
        }

        /// <summary>
        /// Registers a global hotkey
        /// </summary>
        /// <param name="modifiers">Modifier keys (Ctrl, Alt, Shift)</param>
        /// <param name="key">The key to register</param>
        /// <param name="action">Action to execute when hotkey is pressed</param>
        /// <returns>True if registration succeeded</returns>
        public bool RegisterHotkey(ModifierKeys modifiers, Key key, Action action)
        {
            if (_window == null)
                return false;

            int hotkeyId = _nextHotkeyId++;
            var helper = new WindowInteropHelper(_window);
            var hwnd = helper.Handle;

            // Convert WPF key to virtual key code
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);

            // Convert WPF modifiers to Win32 modifiers
            uint mod = 0;
            if (modifiers.HasFlag(ModifierKeys.Control))
                mod |= Win32Helper.MOD_CONTROL;
            if (modifiers.HasFlag(ModifierKeys.Alt))
                mod |= Win32Helper.MOD_ALT;
            if (modifiers.HasFlag(ModifierKeys.Shift))
                mod |= Win32Helper.MOD_SHIFT;
            if (modifiers.HasFlag(ModifierKeys.Windows))
                mod |= Win32Helper.MOD_WIN;

            // Register the hotkey
            bool success = Win32Helper.RegisterHotKey(hwnd, hotkeyId, mod, vk);

            if (success)
            {
                _hotkeyActions[hotkeyId] = action;
            }

            return success;
        }

        /// <summary>
        /// Unregisters all hotkeys
        /// </summary>
        public void UnregisterAll()
        {
            if (_window == null)
                return;

            var helper = new WindowInteropHelper(_window);
            var hwnd = helper.Handle;

            foreach (var hotkeyId in _hotkeyActions.Keys)
            {
                Win32Helper.UnregisterHotKey(hwnd, hotkeyId);
            }

            _hotkeyActions.Clear();
        }

        /// <summary>
        /// Window procedure hook to handle WM_HOTKEY messages
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32Helper.WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();

                if (_hotkeyActions.TryGetValue(hotkeyId, out var action))
                {
                    // Execute the action on the UI thread
                    _window.Dispatcher.Invoke(action);
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Cleans up resources
        /// </summary>
        public void Dispose()
        {
            UnregisterAll();
            _hwndSource?.RemoveHook(WndProc);
        }
    }
}

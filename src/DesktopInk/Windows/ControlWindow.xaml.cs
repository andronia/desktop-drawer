using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using DesktopInk.Core;
using DesktopInk.Infrastructure;

namespace DesktopInk.Windows;

public partial class ControlWindow : Window
{
    private const int HotkeyToggleDraw = 1;
    private const int HotkeyClearAll = 2;
    private const int HotkeyQuit = 3;
    private const double LogicalWidth = 88.0;
    private const double LogicalHeight = 494.0;

    private readonly OverlayManager _overlayManager;
    private readonly AppSettings _appSettings;
    private readonly KeyboardHookManager _keyboardHook;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private uint _dpiX = 96;
    private uint _dpiY = 96;
    private Win32.Rect? _currentMonitorBoundsPx;

    public ControlWindow(OverlayManager overlayManager, AppSettings appSettings)
    {
        _overlayManager = overlayManager;
        _appSettings = appSettings;
        _keyboardHook = new KeyboardHookManager();

        InitializeComponent();

        // Subscribe to mode changes for visual feedback
        _overlayManager.ModeChanged += OnModeChanged;
        _overlayManager.PenColorChanged += OnPenColorChanged;
        _overlayManager.ToolChanged += OnToolChanged;
        _overlayManager.ThicknessChanged += OnThicknessChanged;
        _overlayManager.AutoFadeChanged += OnAutoFadeChanged;
        _overlayManager.SpotlightChanged += OnSpotlightChanged;

        UpdateColorSwatchSelection(_overlayManager.CurrentPenColor);
        UpdateToolButtonAppearance(_overlayManager.CurrentTool);
        UpdateThicknessSelection(_overlayManager.CurrentThickness);
        UpdateFadeButtonAppearance(_overlayManager.IsAutoFadeEnabled);
        UpdateSpotlightButtonAppearance(_overlayManager.IsSpotlightEnabled);

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource.AddHook(WndProc);

        ApplyToolWindowStyle();
        var dpi = Win32.GetDpiForWindow(_hwnd);
        if (dpi != 0)
        {
            _dpiX = dpi;
            _dpiY = dpi;
        }

        if (!TryRestoreSavedPosition())
        {
            PositionNearPrimaryRightEdge();
        }

        LocationChanged += OnLocationChanged;
        UpdateMonitorFromCurrentPosition(forceNotify: true);

        if (!TryRegisterHotkeys())
        {
            System.Windows.MessageBox.Show(
                "Failed to register one or more global hotkeys. Another application may already be using them.",
                "DesktopInk",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // Install keyboard hook for temporary draw mode
        try
        {
            _keyboardHook.TemporaryModeActivated += OnTemporaryModeActivated;
            _keyboardHook.TemporaryModeDeactivated += OnTemporaryModeDeactivated;
            _keyboardHook.ColorCycleRequested += OnColorCycleRequested;
            _keyboardHook.Install();
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to install keyboard hook for temporary draw mode", ex);
        }
    }

    private void ApplyToolWindowStyle()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        // Keep layered (for transparency) but drop the tool-window flag so the palette
        // shows up in the taskbar and Alt+Tab like a normal app window.
        var exStyle = Win32.GetWindowLongPtr(_hwnd, Win32.GwlExStyle).ToInt64();
        exStyle &= ~Win32.WsExToolWindow;
        exStyle |= Win32.WsExLayered;
        Win32.SetWindowLongPtr(_hwnd, Win32.GwlExStyle, new IntPtr(exStyle));
    }

    private bool TryRestoreSavedPosition()
    {
        var saved = _appSettings.Palette;
        if (saved.Left is null || saved.Top is null)
        {
            return false;
        }

        var widthPx = ScaleToPixels(Width, _dpiX);
        var heightPx = ScaleToPixels(Height, _dpiY);
        var leftPx = (int)Math.Round(saved.Left.Value);
        var topPx = (int)Math.Round(saved.Top.Value);

        if (!IsRectOnAnyScreen(leftPx, topPx, widthPx, heightPx))
        {
            // Saved position is off-screen (monitor unplugged, resolution changed).
            return false;
        }

        var boundsPx = new Win32.Rect
        {
            Left = leftPx,
            Top = topPx,
            Right = leftPx + widthPx,
            Bottom = topPx + heightPx,
        };

        ApplyBoundsPxToHwnd(boundsPx);
        ApplyWpfBoundsFromPx(boundsPx, _dpiX, _dpiY);
        return true;
    }

    private static bool IsRectOnAnyScreen(int leftPx, int topPx, int widthPx, int heightPx)
    {
        var rect = new System.Drawing.Rectangle(leftPx, topPx, widthPx, heightPx);
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            if (screen.Bounds.IntersectsWith(rect))
            {
                return true;
            }
        }
        return false;
    }

    private void SaveCurrentPosition()
    {
        if (_hwnd == IntPtr.Zero || !Win32.GetWindowRect(_hwnd, out var rect))
        {
            return;
        }

        _appSettings.Palette.Left = rect.Left;
        _appSettings.Palette.Top = rect.Top;
        _appSettings.Save();
    }

    private void PositionNearPrimaryRightEdge()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        if (primary is null)
        {
            return;
        }

        var workingArea = primary.WorkingArea;
        const int margin = 24;

        // Note: we position in physical pixels for predictable placement.
        var widthPx = ScaleToPixels(Width, _dpiX);
        var heightPx = ScaleToPixels(Height, _dpiY);
        var x = workingArea.Right - widthPx - margin;
        var y = workingArea.Top + margin;

        var boundsPx = new Win32.Rect
        {
            Left = x,
            Top = y,
            Right = x + widthPx,
            Bottom = y + heightPx,
        };

        ApplyBoundsPxToHwnd(boundsPx);
        ApplyWpfBoundsFromPx(boundsPx, _dpiX, _dpiY);
    }

    private bool TryRegisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        var ok = true;
        ok &= Win32.RegisterHotKey(_hwnd, HotkeyToggleDraw, Win32.ModWin | Win32.ModShift, (uint)KeyInterop.VirtualKeyFromKey(Key.D));
        ok &= Win32.RegisterHotKey(_hwnd, HotkeyClearAll, Win32.ModWin | Win32.ModShift, (uint)KeyInterop.VirtualKeyFromKey(Key.C));
        ok &= Win32.RegisterHotKey(_hwnd, HotkeyQuit, Win32.ModWin | Win32.ModShift, (uint)KeyInterop.VirtualKeyFromKey(Key.Q));
        return ok;
    }

    private void UnregisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        Win32.UnregisterHotKey(_hwnd, HotkeyToggleDraw);
        Win32.UnregisterHotKey(_hwnd, HotkeyClearAll);
        Win32.UnregisterHotKey(_hwnd, HotkeyQuit);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SaveCurrentPosition();

        UnregisterHotkeys();

        LocationChanged -= OnLocationChanged;

        _overlayManager.ModeChanged -= OnModeChanged;
        _overlayManager.PenColorChanged -= OnPenColorChanged;
        _overlayManager.ToolChanged -= OnToolChanged;
        _overlayManager.ThicknessChanged -= OnThicknessChanged;
        _overlayManager.AutoFadeChanged -= OnAutoFadeChanged;
        _overlayManager.SpotlightChanged -= OnSpotlightChanged;

        _keyboardHook.TemporaryModeActivated -= OnTemporaryModeActivated;
        _keyboardHook.TemporaryModeDeactivated -= OnTemporaryModeDeactivated;
        _keyboardHook.ColorCycleRequested -= OnColorCycleRequested;
        _keyboardHook.Dispose();

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    private void OnTemporaryModeActivated(object? sender, EventArgs e)
    {
        if (TryGetPaletteMonitorBounds(out var boundsPx))
        {
            _overlayManager.ActivateTemporaryDrawMode(boundsPx);
            return;
        }

        _overlayManager.ActivateTemporaryDrawMode();
    }

    private void OnTemporaryModeDeactivated(object? sender, EventArgs e)
    {
        if (TryGetPaletteMonitorBounds(out var boundsPx))
        {
            _overlayManager.DeactivateTemporaryDrawMode(boundsPx);
            return;
        }

        _overlayManager.DeactivateTemporaryDrawMode();
    }

    private void OnColorCycleRequested(object? sender, EventArgs e)
    {
        _overlayManager.CycleColor();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WmHotkey)
        {
            handled = true;
            var id = wParam.ToInt32();

            switch (id)
            {
                case HotkeyToggleDraw:
                    if (TryGetPaletteMonitorBounds(out var boundsPx))
                    {
                        _overlayManager.ToggleMode(boundsPx);
                    }
                    else
                    {
                        _overlayManager.ToggleMode();
                    }
                    break;
                case HotkeyClearAll:
                    _overlayManager.ClearAll();
                    break;
                case HotkeyQuit:
                    _overlayManager.Quit();
                    break;
            }

            return IntPtr.Zero;
        }

        if (msg != Win32.WmDpichanged)
        {
            return IntPtr.Zero;
        }

        AppLog.Info($"ControlWindow WM_DPICHANGED hwnd=0x{_hwnd.ToInt64():X} wParam=0x{wParam.ToInt64():X} lParam=0x{lParam.ToInt64():X}");

        // New DPI is in wParam (LOWORD=x, HIWORD=y).
        var newDpiX = (uint)(wParam.ToInt32() & 0xFFFF);
        var newDpiY = (uint)((wParam.ToInt32() >> 16) & 0xFFFF);
        if (newDpiX == 0) newDpiX = 96;
        if (newDpiY == 0) newDpiY = 96;

        _dpiX = newDpiX;
        _dpiY = newDpiY;

        // lParam points to a recommended RECT in physical pixels (position only).
        var rect = Marshal.PtrToStructure<Win32.Rect>(lParam);

        var newWidthPx = ScaleToPixels(LogicalWidth, newDpiX);
        var newHeightPx = ScaleToPixels(LogicalHeight, newDpiY);
        var adjustedRect = new Win32.Rect
        {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Left + newWidthPx,
            Bottom = rect.Top + newHeightPx,
        };

        ApplyBoundsPxToHwnd(adjustedRect);
        ApplyWpfBoundsFromPx(adjustedRect, _dpiX, _dpiY);

        handled = true;
        return IntPtr.Zero;
    }

    private void ApplyBoundsPxToHwnd(Win32.Rect boundsPx)
    {
        if (_hwnd != IntPtr.Zero)
        {
            Win32.SetWindowPos(
                _hwnd,
                Win32.HwndTopmost,
                boundsPx.Left,
                boundsPx.Top,
                boundsPx.Width,
                boundsPx.Height,
                Win32.SwpNoActivate);
        }
    }

    private void ApplyWpfBoundsFromPx(Win32.Rect boundsPx, uint dpiX, uint dpiY)
    {
        var dx = dpiX == 0 ? 96u : dpiX;
        var dy = dpiY == 0 ? 96u : dpiY;

        Left = boundsPx.Left * 96.0 / dx;
        Top = boundsPx.Top * 96.0 / dy;
        Width = boundsPx.Width * 96.0 / dx;
        Height = boundsPx.Height * 96.0 / dy;
    }

    private static int ScaleToPixels(double logicalSize, uint dpi)
    {
        var effectiveDpi = dpi == 0 ? 96u : dpi;
        return (int)Math.Round(logicalSize * effectiveDpi / 96.0);
    }

    private void OnToggleClick(object sender, RoutedEventArgs e)
    {
        if (TryGetPaletteMonitorBounds(out var boundsPx))
        {
            _overlayManager.ToggleMode(boundsPx);
            return;
        }

        _overlayManager.ToggleMode();
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        UpdateMonitorFromCurrentPosition();
    }

    private void UpdateMonitorFromCurrentPosition(bool forceNotify = false)
    {
        if (!TryGetCurrentMonitorBounds(out var boundsPx))
        {
            return;
        }

        if (!forceNotify && _currentMonitorBoundsPx.HasValue && AreBoundsEqual(_currentMonitorBoundsPx.Value, boundsPx))
        {
            return;
        }

        _currentMonitorBoundsPx = boundsPx;
        _overlayManager.UpdatePaletteMonitor(boundsPx);
    }

    private bool TryGetPaletteMonitorBounds(out Win32.Rect boundsPx)
    {
        if (_currentMonitorBoundsPx.HasValue)
        {
            boundsPx = _currentMonitorBoundsPx.Value;
            return true;
        }

        return TryGetCurrentMonitorBounds(out boundsPx);
    }

    private bool TryGetCurrentMonitorBounds(out Win32.Rect boundsPx)
    {
        boundsPx = default;

        if (_hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!Win32.GetWindowRect(_hwnd, out var rect))
        {
            return false;
        }

        var centerX = rect.Left + (rect.Width / 2);
        var centerY = rect.Top + (rect.Height / 2);
        var monitor = Win32.MonitorFromPoint(new Win32.Point { X = centerX, Y = centerY }, Win32.MonitorDefaultToNearest);

        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        return Win32.TryGetMonitorBounds(monitor, out boundsPx);
    }

    private static bool AreBoundsEqual(Win32.Rect left, Win32.Rect right)
    {
        return left.Left == right.Left
            && left.Top == right.Top
            && left.Right == right.Right
            && left.Bottom == right.Bottom;
    }

    private void OnColorSwatchClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }

        var color = button.Name switch
        {
            nameof(RedSwatch) => PenColor.Red,
            nameof(BlueSwatch) => PenColor.Blue,
            nameof(GreenSwatch) => PenColor.Green,
            nameof(YellowSwatch) => PenColor.Yellow,
            nameof(WhiteSwatch) => PenColor.White,
            nameof(MagentaSwatch) => PenColor.Magenta,
            nameof(OrangeSwatch) => PenColor.Orange,
            nameof(CyanSwatch) => PenColor.Cyan,
            nameof(BlackSwatch) => PenColor.Black,
            _ => PenColor.Red,
        };

        _overlayManager.SelectColor(color);
    }

    private void OnModeChanged(object? sender, OverlayMode mode)
    {
        UpdateToggleButtonAppearance(mode);
    }

    private void OnPenColorChanged(object? sender, PenColor color)
    {
        UpdateColorSwatchSelection(color);
    }

    private void UpdateToggleButtonAppearance(OverlayMode mode)
    {
        // Update button appearance based on current mode using Tag property
        // This allows XAML style triggers to work properly for hover effects
        ToggleButton.Tag = mode.ToString();
    }

    private void UpdateColorSwatchSelection(PenColor color)
    {
        if (RedSwatch is null)
        {
            return;
        }

        RedSwatch.Tag = color == PenColor.Red ? "Active" : "Inactive";
        BlueSwatch.Tag = color == PenColor.Blue ? "Active" : "Inactive";
        GreenSwatch.Tag = color == PenColor.Green ? "Active" : "Inactive";
        YellowSwatch.Tag = color == PenColor.Yellow ? "Active" : "Inactive";
        WhiteSwatch.Tag = color == PenColor.White ? "Active" : "Inactive";
        MagentaSwatch.Tag = color == PenColor.Magenta ? "Active" : "Inactive";
        OrangeSwatch.Tag = color == PenColor.Orange ? "Active" : "Inactive";
        CyanSwatch.Tag = color == PenColor.Cyan ? "Active" : "Inactive";
        BlackSwatch.Tag = color == PenColor.Black ? "Active" : "Inactive";
    }

    private void OnHighlighterClick(object sender, RoutedEventArgs e)
    {
        _overlayManager.ToggleHighlighter();
    }

    private void OnRectangleClick(object sender, RoutedEventArgs e)
    {
        _overlayManager.ToggleRectangle();
    }

    private void OnArrowClick(object sender, RoutedEventArgs e)
    {
        _overlayManager.ToggleArrow();
    }

    private void OnToolChanged(object? sender, DrawTool tool)
    {
        UpdateToolButtonAppearance(tool);
    }

    private void UpdateToolButtonAppearance(DrawTool tool)
    {
        if (HighlighterButton is null || RectangleButton is null || ArrowButton is null)
        {
            return;
        }

        HighlighterButton.Tag = tool == DrawTool.Highlighter ? "Active" : "Inactive";
        RectangleButton.Tag = tool == DrawTool.Rectangle ? "Active" : "Inactive";
        ArrowButton.Tag = tool == DrawTool.Arrow ? "Active" : "Inactive";
    }

    private void OnThicknessSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // ValueChanged fires during InitializeComponent before the manager hook-up, so guard.
        if (_overlayManager is null)
        {
            return;
        }

        var thickness = (int)Math.Round(e.NewValue);
        _overlayManager.SelectThickness(thickness);
    }

    private void OnThicknessSliderWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var step = e.Delta > 0 ? 1 : -1;
        ThicknessSlider.Value = Math.Clamp(ThicknessSlider.Value + step, ThicknessSlider.Minimum, ThicknessSlider.Maximum);
        e.Handled = true;
    }

    private void OnThicknessChanged(object? sender, int thickness)
    {
        UpdateThicknessSelection(thickness);
    }

    private void UpdateThicknessSelection(int thickness)
    {
        if (ThicknessSlider is null)
        {
            return;
        }

        if ((int)Math.Round(ThicknessSlider.Value) != thickness)
        {
            ThicknessSlider.Value = thickness;
        }
    }

    private void OnFadeClick(object sender, RoutedEventArgs e)
    {
        _overlayManager.ToggleAutoFade();
    }

    private void OnAutoFadeChanged(object? sender, bool enabled)
    {
        UpdateFadeButtonAppearance(enabled);
    }

    private void UpdateFadeButtonAppearance(bool enabled)
    {
        if (FadeButton is null)
        {
            return;
        }

        FadeButton.Tag = enabled ? "Active" : "Inactive";
    }

    private void OnSpotlightClick(object sender, RoutedEventArgs e)
    {
        _overlayManager.ToggleSpotlight();
    }

    private void OnSpotlightChanged(object? sender, bool enabled)
    {
        UpdateSpotlightButtonAppearance(enabled);
    }

    private void UpdateSpotlightButtonAppearance(bool enabled)
    {
        if (SpotlightButton is null)
        {
            return;
        }

        SpotlightButton.Tag = enabled ? "Active" : "Inactive";
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _overlayManager.ClearAll();
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        _overlayManager.Quit();
    }
}

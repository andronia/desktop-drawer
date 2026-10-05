using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using DesktopInk.Core;
using DesktopInk.Infrastructure;

namespace DesktopInk.Windows;

public partial class ControlWindow : Window
{
    private const double DefaultEdgeMarginDip = 24.0;

    private readonly OverlayManager _overlayManager;
    private readonly AppSettings _appSettings;
    private readonly KeyboardHookManager _keyboardHook;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;
    private GlobalHotkeyRegistrar? _hotkeys;
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
        _overlayManager.OverlaysRefreshed += OnOverlaysRefreshed;

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
                // DragMove blocks until the mouse is released.
                DragMove();
                SaveCurrentPosition();
            }
        };
    }

    /// <summary>Actions whose global hotkey could not be registered (all candidates taken).</summary>
    public IReadOnlyList<HotkeyAction> UnboundHotkeys => _hotkeys?.Unbound ?? Array.Empty<HotkeyAction>();

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource.AddHook(WndProc);

        ApplyToolWindowStyle();

        if (!TryRestoreSavedPosition())
        {
            PositionNearPrimaryRightEdge();
        }

        LocationChanged += OnLocationChanged;
        UpdateMonitorFromCurrentPosition(forceNotify: true);

        _hotkeys = new GlobalHotkeyRegistrar(_hwnd);
        _hotkeys.RegisterAll(_appSettings.Hotkeys);
        ApplyHotkeyTooltips();

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

    private void ApplyHotkeyTooltips()
    {
        ToggleButton.ToolTip = $"Toggle Draw{FormatHotkey(HotkeyAction.ToggleDraw)}\nAlt+Double-click for temporary mode";
        ClearButton.ToolTip = $"Clear{FormatHotkey(HotkeyAction.ClearAll)}";
        QuitButton.ToolTip = $"Quit{FormatHotkey(HotkeyAction.Quit)}";
    }

    private string FormatHotkey(HotkeyAction action)
    {
        return _hotkeys is not null && _hotkeys.Bound.TryGetValue(action, out var gesture)
            ? $" ({gesture})"
            : string.Empty;
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

        var leftPx = (int)Math.Round(saved.Left.Value);
        var topPx = (int)Math.Round(saved.Top.Value);

        // Nearest monitor to the saved corner: handles unplugged monitors and layout changes.
        var monitor = Win32.MonitorFromPoint(new Win32.Point { X = leftPx, Y = topPx }, Win32.MonitorDefaultToNearest);
        return PlaceOnMonitor(monitor, leftPx, topPx);
    }

    private void PositionNearPrimaryRightEdge()
    {
        // The primary monitor always contains the desktop origin.
        var monitor = Win32.MonitorFromPoint(new Win32.Point { X = 0, Y = 0 }, Win32.MonitorDefaultToPrimary);
        if (!Win32.TryGetMonitorWorkArea(monitor, out var workArea))
        {
            return;
        }

        Win32.TryGetMonitorDpi(monitor, out var dpi, out _);
        var widthPx = ScaleToPixels(Width, dpi);
        var marginPx = ScaleToPixels(DefaultEdgeMarginDip, dpi);

        PlaceOnMonitor(monitor, workArea.Right - widthPx - marginPx, workArea.Top + marginPx);
    }

    /// <summary>
    /// Moves the palette's top-left corner to the given physical pixel position, clamped so the
    /// whole palette stays inside the monitor's work area at that monitor's DPI.
    /// </summary>
    private bool PlaceOnMonitor(IntPtr monitor, int leftPx, int topPx)
    {
        if (_hwnd == IntPtr.Zero || monitor == IntPtr.Zero || !Win32.TryGetMonitorWorkArea(monitor, out var workArea))
        {
            return false;
        }

        Win32.TryGetMonitorDpi(monitor, out var dpi, out _);
        var widthPx = ScaleToPixels(Width, dpi);
        var heightPx = ScaleToPixels(Height, dpi);

        var x = Math.Max(workArea.Left, Math.Min(leftPx, workArea.Right - widthPx));
        var y = Math.Max(workArea.Top, Math.Min(topPx, workArea.Bottom - heightPx));

        MoveTopLeftPx(x, y);
        return true;
    }

    private void MoveTopLeftPx(int leftPx, int topPx)
    {
        // Position only: WPF resizes the window itself when the move changes its DPI. That
        // resize is anchored on Windows' suggested rect, so re-apply the corner if it drifted.
        const uint flags = Win32.SwpNoSize | Win32.SwpNoZOrder | Win32.SwpNoActivate;
        Win32.SetWindowPos(_hwnd, IntPtr.Zero, leftPx, topPx, 0, 0, flags);

        if (Win32.GetWindowRect(_hwnd, out var rect) && (rect.Left != leftPx || rect.Top != topPx))
        {
            Win32.SetWindowPos(_hwnd, IntPtr.Zero, leftPx, topPx, 0, 0, flags);
        }
    }

    private void EnsureOnScreen()
    {
        if (_hwnd == IntPtr.Zero || !Win32.GetWindowRect(_hwnd, out var rect))
        {
            return;
        }

        var monitor = Win32.MonitorFromWindow(_hwnd, Win32.MonitorDefaultToNearest);
        PlaceOnMonitor(monitor, rect.Left, rect.Top);
    }

    private void BringAboveOverlays()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        Win32.SetWindowPos(
            _hwnd,
            Win32.HwndTopmost,
            0,
            0,
            0,
            0,
            Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoActivate);
    }

    private void OnOverlaysRefreshed(object? sender, EventArgs e)
    {
        // Monitors were added, removed or rearranged: keep the palette reachable, above the
        // freshly created overlays, and re-bind draw mode to whichever monitor it is now on.
        EnsureOnScreen();
        BringAboveOverlays();
        UpdateMonitorFromCurrentPosition(forceNotify: true);
    }

    private void SaveCurrentPosition()
    {
        if (_hwnd == IntPtr.Zero || !Win32.GetWindowRect(_hwnd, out var rect))
        {
            return;
        }

        if (_appSettings.Palette.Left == rect.Left && _appSettings.Palette.Top == rect.Top)
        {
            return;
        }

        _appSettings.Palette.Left = rect.Left;
        _appSettings.Palette.Top = rect.Top;
        _appSettings.Save();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        SaveCurrentPosition();

        _hotkeys?.UnregisterAll();

        LocationChanged -= OnLocationChanged;

        _overlayManager.ModeChanged -= OnModeChanged;
        _overlayManager.PenColorChanged -= OnPenColorChanged;
        _overlayManager.ToolChanged -= OnToolChanged;
        _overlayManager.ThicknessChanged -= OnThicknessChanged;
        _overlayManager.AutoFadeChanged -= OnAutoFadeChanged;
        _overlayManager.SpotlightChanged -= OnSpotlightChanged;
        _overlayManager.OverlaysRefreshed -= OnOverlaysRefreshed;

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
        // WM_DPICHANGED is deliberately left to WPF: it rescales the content and resizes the
        // window to keep the palette's logical size when it is dragged across monitors.
        if (msg != Win32.WmHotkey)
        {
            return IntPtr.Zero;
        }

        handled = true;

        switch ((HotkeyAction)wParam.ToInt32())
        {
            case HotkeyAction.ToggleDraw:
                OnToggleClick(this, new RoutedEventArgs());
                break;
            case HotkeyAction.ClearAll:
                _overlayManager.ClearAll();
                break;
            case HotkeyAction.Quit:
                _overlayManager.Quit();
                break;
        }

        return IntPtr.Zero;
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

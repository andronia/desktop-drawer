using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DesktopInk.Core;
using DesktopInk.Infrastructure;

namespace DesktopInk.Windows;

public partial class OverlayWindow : Window, IOverlayWindow
{
    private Win32.Rect _boundsPx;

    private uint _dpiX;
    private uint _dpiY;

    private IntPtr _hwnd;
    private HwndSource? _hwndSource;

    private OverlayMode _mode = OverlayMode.PassThrough;
    private bool _isDrawing;
    private Polyline? _activeStroke;
    private System.Windows.Shapes.Rectangle? _activeRect;
    private System.Windows.Point _strokeStartPoint;
    private PenColor _penColor = PenColor.Red;
    private DrawTool _tool = DrawTool.Pen;
    private PenThickness _thickness = PenThickness.Medium;
    private bool _autoFadeEnabled;

    private static readonly TimeSpan AutoFadeHold = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan AutoFadeDuration = TimeSpan.FromMilliseconds(2300);

    internal OverlayWindow(Win32.Rect boundsPx, uint dpiX, uint dpiY)
    {
        _boundsPx = boundsPx;
        _dpiX = dpiX == 0 ? 96u : dpiX;
        _dpiY = dpiY == 0 ? 96u : dpiY;

        InitializeComponent();

        ApplyWpfBoundsFromPx(_boundsPx, _dpiX, _dpiY);

        AppLog.Info($"OverlayWindow ctor boundsPx=({_boundsPx.Left},{_boundsPx.Top}) {_boundsPx.Width}x{_boundsPx.Height} dpi=({_dpiX},{_dpiY})");

        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    public Win32.Rect MonitorBoundsPx => _boundsPx;

    public void SetMode(OverlayMode mode, bool isTemporary = false)
    {
        _mode = mode;

        AppLog.Info($"OverlayWindow SetMode hwnd=0x{_hwnd.ToInt64():X} mode={_mode} isTemporary={isTemporary}");

        if (_mode == OverlayMode.Draw)
        {
            SetClickThrough(false);
            IndicatorHost.Visibility = Visibility.Visible;
            IndicatorText.Text = isTemporary ? "DRAW (TEMP)" : "DRAW";
            IndicatorText.Foreground = CreatePenBrush(_penColor);
        }
        else
        {
            CancelActiveStroke();
            SetClickThrough(true);
            IndicatorHost.Visibility = Visibility.Collapsed;
        }

        LogGeometry("after-set-mode");
    }

    public void ClearAll()
    {
        StrokeCanvas.Children.Clear();
    }

    public void SetPenColor(PenColor color)
    {
        _penColor = color;

        if (_mode == OverlayMode.Draw)
        {
            IndicatorText.Foreground = CreatePenBrush(_penColor);
        }
    }

    public void SetTool(DrawTool tool)
    {
        _tool = tool;
    }

    public void SetThickness(PenThickness thickness)
    {
        _thickness = thickness;
    }

    public void SetAutoFade(bool enabled)
    {
        _autoFadeEnabled = enabled;
    }

    private void OnSourceInitialized(object? sender, System.EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource.AddHook(WndProc);

        ApplyToolWindowStyle();
        // Ensure WPF logical bounds match the actual monitor bounds.
        // Use the window DPI as ground truth if available.
        var dpi = Win32.GetDpiForWindow(_hwnd);
        if (dpi != 0)
        {
            _dpiX = dpi;
            _dpiY = dpi;
        }

        ApplyWpfBoundsFromPx(_boundsPx, _dpiX, _dpiY);

        SetMode(_mode);

        LogGeometry("after-source-init");
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

    private void ApplyBoundsPxToHwnd(Win32.Rect boundsPx)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        Win32.SetWindowPos(
            _hwnd,
            Win32.HwndTopmost,
            boundsPx.Left,
            boundsPx.Top,
            boundsPx.Width,
            boundsPx.Height,
            Win32.SwpNoActivate);
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WmMouseActivate)
        {
            // In draw mode we want to receive pointer input, but we do not want to steal activation
            // from the control palette or underlying apps.
            if (_mode == OverlayMode.Draw)
            {
                handled = true;
                return new IntPtr(Win32.MaNoActivate);
            }

            return IntPtr.Zero;
        }

        if (msg != Win32.WmDpichanged)
        {
            return IntPtr.Zero;
        }

        AppLog.Info($"OverlayWindow WM_DPICHANGED hwnd=0x{_hwnd.ToInt64():X} wParam=0x{wParam.ToInt64():X} lParam=0x{lParam.ToInt64():X}");

        // New DPI is in wParam (LOWORD=x, HIWORD=y).
        var newDpiX = (uint)(wParam.ToInt32() & 0xFFFF);
        var newDpiY = (uint)((wParam.ToInt32() >> 16) & 0xFFFF);
        if (newDpiX == 0) newDpiX = 96;
        if (newDpiY == 0) newDpiY = 96;

        _dpiX = newDpiX;
        _dpiY = newDpiY;

        // lParam points to a recommended RECT in physical pixels.
        var rect = Marshal.PtrToStructure<Win32.Rect>(lParam);
        _boundsPx = rect;

        ApplyBoundsPxToHwnd(rect);
        ApplyWpfBoundsFromPx(rect, _dpiX, _dpiY);

        LogGeometry("wm-dpichanged");

        handled = true;
        return IntPtr.Zero;
    }

    private void ApplyToolWindowStyle()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var exStyle = Win32.GetWindowLongPtr(_hwnd, Win32.GwlExStyle).ToInt64();
        exStyle |= Win32.WsExToolWindow;
        exStyle |= Win32.WsExLayered;
        Win32.SetWindowLongPtr(_hwnd, Win32.GwlExStyle, new IntPtr(exStyle));

        AppLog.Info($"OverlayWindow ToolWindowStyle hwnd=0x{_hwnd.ToInt64():X} exStyle=0x{exStyle:X}");
    }

    private void SetClickThrough(bool enabled)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var exStyle = Win32.GetWindowLongPtr(_hwnd, Win32.GwlExStyle).ToInt64();
        var before = exStyle;

        exStyle |= Win32.WsExLayered;
        exStyle |= Win32.WsExToolWindow;

        if (enabled)
        {
            exStyle |= Win32.WsExTransparent;
            Root.IsHitTestVisible = false;
        }
        else
        {
            exStyle &= ~Win32.WsExTransparent;
            Root.IsHitTestVisible = true;
        }

        Win32.SetWindowLongPtr(_hwnd, Win32.GwlExStyle, new IntPtr(exStyle));

        _ = Win32.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            Win32.SwpNoActivate | Win32.SwpNoMove | Win32.SwpNoSize | Win32.SwpNoZOrder | Win32.SwpFrameChanged);

        AppLog.Info($"OverlayWindow ClickThrough hwnd=0x{_hwnd.ToInt64():X} enabled={enabled} exStyleBefore=0x{before:X} exStyleAfter=0x{exStyle:X} hitTest={Root.IsHitTestVisible}");
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_mode != OverlayMode.Draw)
        {
            return;
        }

        var point = e.GetPosition(StrokeCanvas);
        _strokeStartPoint = point;
        _isDrawing = true;

        if (_tool == DrawTool.Rectangle)
        {
            _activeRect = CreateRectangle(_penColor, _thickness);
            System.Windows.Controls.Canvas.SetLeft(_activeRect, point.X);
            System.Windows.Controls.Canvas.SetTop(_activeRect, point.Y);
            StrokeCanvas.Children.Add(_activeRect);
        }
        else
        {
            _activeStroke = CreateStroke(_penColor, _tool, _thickness);
            _activeStroke.Points.Add(point);
            StrokeCanvas.Children.Add(_activeStroke);
        }

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_mode != OverlayMode.Draw || !_isDrawing)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            CancelActiveStroke();
            return;
        }

        var point = e.GetPosition(StrokeCanvas);
        var isShiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        if (_tool == DrawTool.Rectangle && _activeRect is not null)
        {
            UpdateActiveRectangle(point, isShiftHeld);
            e.Handled = true;
            return;
        }

        if (_activeStroke is null)
        {
            return;
        }

        if (isShiftHeld)
        {
            point = ApplyStraightLineConstraint(point);

            if (_activeStroke.Points.Count > 1)
            {
                _activeStroke.Points.RemoveAt(_activeStroke.Points.Count - 1);
            }
        }

        _activeStroke.Points.Add(point);
        e.Handled = true;
    }

    private void UpdateActiveRectangle(System.Windows.Point current, bool squareConstraint)
    {
        if (_activeRect is null)
        {
            return;
        }

        var start = _strokeStartPoint;
        var width = Math.Abs(current.X - start.X);
        var height = Math.Abs(current.Y - start.Y);

        if (squareConstraint)
        {
            var size = Math.Max(width, height);
            width = size;
            height = size;
        }

        var left = current.X < start.X ? start.X - width : start.X;
        var top = current.Y < start.Y ? start.Y - height : start.Y;

        System.Windows.Controls.Canvas.SetLeft(_activeRect, left);
        System.Windows.Controls.Canvas.SetTop(_activeRect, top);
        _activeRect.Width = width;
        _activeRect.Height = height;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_mode != OverlayMode.Draw)
        {
            return;
        }

        try
        {
            var p = e.GetPosition(StrokeCanvas);
            AppLog.Info($"OverlayWindow MouseUp hwnd=0x{_hwnd.ToInt64():X} p=({p.X:0.##},{p.Y:0.##})");
        }
        catch { }

        if (_isDrawing)
        {
            _isDrawing = false;
            UIElement? completed = _activeRect ?? (UIElement?)_activeStroke;
            _activeStroke = null;
            _activeRect = null;
            ReleaseMouseCapture();

            if (_autoFadeEnabled && completed is not null)
            {
                StartFadeOut(completed);
            }

            e.Handled = true;
        }
    }

    private void StartFadeOut(UIElement element)
    {
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            BeginTime = AutoFadeHold,
            Duration = new Duration(AutoFadeDuration),
        };

        animation.Completed += (_, _) =>
        {
            // The stroke may already be gone if ClearAll ran mid-fade; Remove is safe in that case.
            StrokeCanvas.Children.Remove(element);
        };

        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void CancelActiveStroke()
    {
        _isDrawing = false;
        if (_activeRect is not null)
        {
            StrokeCanvas.Children.Remove(_activeRect);
            _activeRect = null;
        }
        _activeStroke = null;
        ReleaseMouseCapture();
    }

    private System.Windows.Point ApplyStraightLineConstraint(System.Windows.Point currentPoint)
    {
        // Calculate the distance and angle from stroke start to current point
        var dx = currentPoint.X - _strokeStartPoint.X;
        var dy = currentPoint.Y - _strokeStartPoint.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        
        // Minimum distance threshold to avoid jittery behavior on small drags
        if (distance < 5.0)
        {
            return currentPoint;
        }
        
        // Calculate angle in radians
        var angle = Math.Atan2(Math.Abs(dy), Math.Abs(dx));
        
        // 45-degree threshold: if angle < 45° (π/4), snap to horizontal; otherwise vertical
        if (angle < Math.PI / 4)
        {
            // Horizontal constraint: keep Y, vary X
            return new System.Windows.Point(currentPoint.X, _strokeStartPoint.Y);
        }
        else
        {
            // Vertical constraint: keep X, vary Y
            return new System.Windows.Point(_strokeStartPoint.X, currentPoint.Y);
        }
    }

    private static System.Windows.Shapes.Rectangle CreateRectangle(PenColor penColor, PenThickness thickness)
    {
        return new System.Windows.Shapes.Rectangle
        {
            Stroke = CreatePenBrush(penColor),
            StrokeThickness = PenWidth(thickness),
            Fill = System.Windows.Media.Brushes.Transparent,
            SnapsToDevicePixels = true,
        };
    }

    private static Polyline CreateStroke(PenColor penColor, DrawTool tool, PenThickness thickness)
    {
        var brush = CreatePenBrush(penColor);

        if (tool == DrawTool.Highlighter)
        {
            brush.Opacity = 0.35;
            return new Polyline
            {
                Stroke = brush,
                StrokeThickness = HighlighterWidth(thickness),
                StrokeStartLineCap = PenLineCap.Flat,
                StrokeEndLineCap = PenLineCap.Flat,
                StrokeLineJoin = PenLineJoin.Round,
                SnapsToDevicePixels = true,
            };
        }

        return new Polyline
        {
            Stroke = brush,
            StrokeThickness = PenWidth(thickness),
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            SnapsToDevicePixels = true,
        };
    }

    private static double PenWidth(PenThickness thickness) => thickness switch
    {
        PenThickness.Thin => 2.0,
        PenThickness.Thick => 7.0,
        _ => 4.0,
    };

    private static double HighlighterWidth(PenThickness thickness) => thickness switch
    {
        PenThickness.Thin => 12.0,
        PenThickness.Thick => 28.0,
        _ => 18.0,
    };

    private static SolidColorBrush CreatePenBrush(PenColor penColor)
    {
        return penColor switch
        {
            PenColor.Red => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x20, 0x20)),
            PenColor.Blue => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x6F, 0xFF)),
            PenColor.Green => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0xDD, 0x55)),
            PenColor.Yellow => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xE6, 0x1A)),
            PenColor.White => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xFF, 0xFF)),
            PenColor.Magenta => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x2E, 0xB5)),
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x20, 0x20)),
        };
    }

    private void LogGeometry(string tag)
    {
        try
        {
            var dpiWin = _hwnd != IntPtr.Zero ? Win32.GetDpiForWindow(_hwnd) : 0;
            var wpf = $"wpf L/T=({Left:0.##},{Top:0.##}) W/H=({Width:0.##},{Height:0.##}) Actual=({ActualWidth:0.##},{ActualHeight:0.##})";

            var hwndRect = "hwndRect=(n/a)";
            if (_hwnd != IntPtr.Zero && Win32.GetWindowRect(_hwnd, out var r))
            {
                hwndRect = $"hwndRect=({r.Left},{r.Top}) {r.Width}x{r.Height}";
            }

            AppLog.Info($"OverlayWindow Geometry {tag} hwnd=0x{_hwnd.ToInt64():X} boundsPx=({_boundsPx.Left},{_boundsPx.Top}) {_boundsPx.Width}x{_boundsPx.Height} dpi=({_dpiX},{_dpiY}) dpiWin={dpiWin} {hwndRect} {wpf}");
        }
        catch (Exception ex)
        {
            AppLog.Error("OverlayWindow LogGeometry failed", ex);
        }
    }
}

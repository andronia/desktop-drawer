using DesktopInk.Infrastructure;

namespace DesktopInk.Core;

internal interface IOverlayWindow
{
    Win32.Rect MonitorBoundsPx { get; }

    void SetMode(OverlayMode mode, bool isTemporary = false);

    void ClearAll();

    void SetPenColor(PenColor color);

    void SetTool(DrawTool tool);

    void SetThickness(int thickness);

    void SetAutoFade(bool enabled);

    void SetSpotlight(bool enabled);

    void Show();

    void Close();
}

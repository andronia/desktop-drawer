using System;
using System.Drawing;
using System.Windows;
using DesktopInk.Core;
using DesktopInk.Windows;
using WinForms = System.Windows.Forms;

namespace DesktopInk.Infrastructure;

/// <summary>
/// System-tray presence for DesktopInk. Shows an icon in the notification area with a
/// context menu for the main actions. Double-click toggles the palette visibility.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly OverlayManager _overlayManager;
    private readonly ControlWindow _controlWindow;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconManager(OverlayManager overlayManager, ControlWindow controlWindow)
    {
        _overlayManager = overlayManager;
        _controlWindow = controlWindow;

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "DesktopInk",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(),
        };

        _notifyIcon.DoubleClick += (_, _) => TogglePaletteVisibility();
    }

    private WinForms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Show/Hide Palette", image: null, (_, _) => TogglePaletteVisibility());
        menu.Items.Add("Toggle Draw Mode", image: null, (_, _) => _overlayManager.ToggleMode());
        menu.Items.Add("Clear All", image: null, (_, _) => _overlayManager.ClearAll());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Quit DesktopInk", image: null, (_, _) => _overlayManager.Quit());
        return menu;
    }

    public void ShowPalette()
    {
        if (!_controlWindow.IsVisible)
        {
            _controlWindow.Show();
        }

        _controlWindow.Activate();
    }

    private void TogglePaletteVisibility()
    {
        if (_controlWindow.IsVisible)
        {
            _controlWindow.Hide();
        }
        else
        {
            _controlWindow.Show();
            _controlWindow.Activate();
        }
    }

    private static Icon LoadIcon()
    {
        try
        {
            var resource = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute));
            if (resource?.Stream is not null)
            {
                return new Icon(resource.Stream);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("TrayIconManager failed to load embedded icon; falling back to system icon.", ex);
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}

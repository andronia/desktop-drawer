using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DesktopInk.Core;

namespace DesktopInk.Infrastructure;

/// <summary>
/// Registers one global hotkey per <see cref="HotkeyAction"/>, trying each action's
/// candidate gestures in order. Hotkey ids are the <see cref="HotkeyAction"/> values,
/// so WM_HOTKEY's wParam maps straight back to the action.
/// </summary>
internal sealed class GlobalHotkeyRegistrar
{
    private readonly Func<HotkeyGesture, int, bool> _register;
    private readonly Action<int> _unregister;
    private readonly Dictionary<HotkeyAction, HotkeyGesture> _bound = new();
    private readonly List<HotkeyAction> _unbound = new();

    public GlobalHotkeyRegistrar(IntPtr hwnd)
        : this(
            (gesture, id) => Win32.RegisterHotKey(hwnd, id, gesture.Modifiers | Win32.ModNoRepeat, gesture.VirtualKey),
            id => Win32.UnregisterHotKey(hwnd, id))
    {
    }

    internal GlobalHotkeyRegistrar(Func<HotkeyGesture, int, bool> register, Action<int> unregister)
    {
        _register = register;
        _unregister = unregister;
    }

    public IReadOnlyDictionary<HotkeyAction, HotkeyGesture> Bound => _bound;

    /// <summary>Actions for which no candidate could be registered.</summary>
    public IReadOnlyList<HotkeyAction> Unbound => _unbound;

    public void RegisterAll(HotkeySettings settings)
    {
        UnregisterAll();

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (TryRegister(action, settings.GetCandidates(action), out var gesture))
            {
                _bound[action] = gesture;
                AppLog.Info($"Hotkey {action} bound to {gesture}.");
            }
            else
            {
                _unbound.Add(action);
                AppLog.Error($"Hotkey {action}: none of [{string.Join(", ", settings.GetCandidates(action))}] could be registered.");
            }
        }
    }

    public void UnregisterAll()
    {
        foreach (var action in _bound.Keys)
        {
            _unregister((int)action);
        }

        _bound.Clear();
        _unbound.Clear();
    }

    private bool TryRegister(HotkeyAction action, IReadOnlyList<string> candidates, out HotkeyGesture registered)
    {
        foreach (var text in candidates)
        {
            if (!HotkeyGesture.TryParse(text, out var gesture))
            {
                AppLog.Error($"Hotkey {action}: '{text}' is not a valid shortcut.");
                continue;
            }

            if (_register(gesture, (int)action))
            {
                registered = gesture;
                return true;
            }

            AppLog.Info($"Hotkey {action}: {gesture} unavailable (error {Marshal.GetLastPInvokeError()}).");
        }

        registered = default;
        return false;
    }
}

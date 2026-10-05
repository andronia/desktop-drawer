using System;
using System.Collections.Generic;
using System.Windows.Input;
using DesktopInk.Infrastructure;

namespace DesktopInk.Core;

/// <summary>
/// A global hotkey in the form RegisterHotKey expects (MOD_* flags + virtual key),
/// parsed from and formatted to text such as "Win+Shift+D".
/// </summary>
public readonly record struct HotkeyGesture(uint Modifiers, Key Key)
{
    public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        uint modifiers = 0;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var modifier = ParseModifier(parts[i]);
            if (modifier == 0 || (modifiers & modifier) != 0)
            {
                return false;
            }

            modifiers |= modifier;
        }

        if (!TryParseKey(parts[^1], out var key))
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(5);
        if ((Modifiers & Win32.ModControl) != 0) parts.Add("Ctrl");
        if ((Modifiers & Win32.ModAlt) != 0) parts.Add("Alt");
        if ((Modifiers & Win32.ModShift) != 0) parts.Add("Shift");
        if ((Modifiers & Win32.ModWin) != 0) parts.Add("Win");

        // Win is conventionally written first ("Win+Shift+D").
        if (parts.Remove("Win"))
        {
            parts.Insert(0, "Win");
        }

        parts.Add(FormatKey(Key));
        return string.Join('+', parts);
    }

    private static uint ParseModifier(string token)
    {
        return token.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => Win32.ModControl,
            "ALT" => Win32.ModAlt,
            "SHIFT" => Win32.ModShift,
            "WIN" or "WINDOWS" => Win32.ModWin,
            _ => 0,
        };
    }

    private static bool TryParseKey(string token, out Key key)
    {
        key = Key.None;

        // Single digits map to the top-row keys (Key.D0..D9), not numpad.
        if (token.Length == 1 && char.IsAsciiDigit(token[0]))
        {
            key = Key.D0 + (token[0] - '0');
            return true;
        }

        if (token.Length == 1 && char.IsAsciiLetter(token[0]))
        {
            key = Key.A + (char.ToUpperInvariant(token[0]) - 'A');
            return true;
        }

        // Named keys (F1..F24, Delete, Back, Space, Home, ...). Numeric strings are
        // rejected because Enum.TryParse would accept them as raw enum values.
        if (!int.TryParse(token, out _) &&
            Enum.TryParse(token, ignoreCase: true, out key) &&
            Enum.IsDefined(key) &&
            key is not (Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System))
        {
            return true;
        }

        key = Key.None;
        return false;
    }

    private static string FormatKey(Key key)
    {
        return key is >= Key.D0 and <= Key.D9
            ? ((char)('0' + (key - Key.D0))).ToString()
            : key.ToString();
    }
}

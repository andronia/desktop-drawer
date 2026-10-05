using System.Windows.Input;
using DesktopInk.Core;
using DesktopInk.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DesktopInk.Tests.Core;

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("Win+Shift+D", Win32.ModWin | Win32.ModShift, Key.D)]
    [InlineData("ctrl + alt + shift + c", Win32.ModControl | Win32.ModAlt | Win32.ModShift, Key.C)]
    [InlineData("Control+Windows+1", Win32.ModControl | Win32.ModWin, Key.D1)]
    [InlineData("Win+Shift+Delete", Win32.ModWin | Win32.ModShift, Key.Delete)]
    [InlineData("F9", 0u, Key.F9)]
    public void TryParse_ShouldAcceptValidGestures(string text, uint expectedModifiers, Key expectedKey)
    {
        HotkeyGesture.TryParse(text, out var gesture).Should().BeTrue();

        gesture.Modifiers.Should().Be(expectedModifiers);
        gesture.Key.Should().Be(expectedKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Win+Shift")]
    [InlineData("Hyper+D")]
    [InlineData("Win+Win+D")]
    [InlineData("Win+Shift+NotAKey")]
    [InlineData("Win+42")]
    public void TryParse_ShouldRejectInvalidGestures(string? text)
    {
        HotkeyGesture.TryParse(text, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("shift+win+d", "Win+Shift+D")]
    [InlineData("Alt+Ctrl+Shift+Q", "Ctrl+Alt+Shift+Q")]
    [InlineData("Win+Shift+1", "Win+Shift+1")]
    public void ToString_ShouldUseCanonicalOrder(string text, string expected)
    {
        HotkeyGesture.TryParse(text, out var gesture).Should().BeTrue();

        gesture.ToString().Should().Be(expected);
    }
}

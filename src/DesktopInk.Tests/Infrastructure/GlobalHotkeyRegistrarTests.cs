using DesktopInk.Core;
using DesktopInk.Infrastructure;
using FluentAssertions;
using Xunit;

namespace DesktopInk.Tests.Infrastructure;

public class GlobalHotkeyRegistrarTests
{
    [Fact]
    public void RegisterAll_ShouldUsePrimaryDefaults_WhenAllAreFree()
    {
        var registrar = CreateRegistrar(taken: [], out _);

        registrar.RegisterAll(new HotkeySettings());

        registrar.Bound[HotkeyAction.ToggleDraw].ToString().Should().Be("Win+Shift+D");
        registrar.Bound[HotkeyAction.ClearAll].ToString().Should().Be("Win+Shift+C");
        registrar.Bound[HotkeyAction.Quit].ToString().Should().Be("Win+Shift+Q");
        registrar.Unbound.Should().BeEmpty();
    }

    [Fact]
    public void RegisterAll_ShouldFallBack_WhenDefaultIsTaken()
    {
        var registrar = CreateRegistrar(taken: ["Win+Shift+C"], out _);

        registrar.RegisterAll(new HotkeySettings());

        registrar.Bound[HotkeyAction.ClearAll].ToString().Should().Be("Win+Shift+X");
        registrar.Unbound.Should().BeEmpty();
    }

    [Fact]
    public void RegisterAll_ShouldReportUnbound_WhenConfiguredGestureIsTaken()
    {
        var registrar = CreateRegistrar(taken: ["Win+Shift+C"], out _);

        registrar.RegisterAll(new HotkeySettings { ClearAll = "Win+Shift+C" });

        registrar.Bound.Should().NotContainKey(HotkeyAction.ClearAll);
        registrar.Unbound.Should().Equal(HotkeyAction.ClearAll);
    }

    [Fact]
    public void RegisterAll_ShouldSkipUnparseableConfiguredGesture()
    {
        var registrar = CreateRegistrar(taken: [], out _);

        registrar.RegisterAll(new HotkeySettings { Quit = "Win+Banana" });

        registrar.Unbound.Should().Equal(HotkeyAction.Quit);
    }

    [Fact]
    public void UnregisterAll_ShouldReleaseEveryBoundHotkey()
    {
        var registrar = CreateRegistrar(taken: [], out var registered);
        registrar.RegisterAll(new HotkeySettings());

        registrar.UnregisterAll();

        registered.Should().BeEmpty();
        registrar.Bound.Should().BeEmpty();
    }

    private static GlobalHotkeyRegistrar CreateRegistrar(string[] taken, out HashSet<int> registeredIds)
    {
        var ids = new HashSet<int>();
        registeredIds = ids;

        return new GlobalHotkeyRegistrar(
            (gesture, id) =>
            {
                if (taken.Contains(gesture.ToString()))
                {
                    return false;
                }

                ids.Add(id);
                return true;
            },
            id => ids.Remove(id));
    }
}

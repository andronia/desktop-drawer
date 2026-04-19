using DesktopInk.Core;
using FluentAssertions;
using Xunit;

namespace DesktopInk.Tests.Core;

public class PenColorTests
{
    [Fact]
    public void PenColor_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<PenColor>();

        values.Should().HaveCount(9);
        values.Should().Contain(PenColor.Red);
        values.Should().Contain(PenColor.Blue);
        values.Should().Contain(PenColor.Green);
        values.Should().Contain(PenColor.Yellow);
        values.Should().Contain(PenColor.White);
        values.Should().Contain(PenColor.Magenta);
        values.Should().Contain(PenColor.Orange);
        values.Should().Contain(PenColor.Cyan);
        values.Should().Contain(PenColor.Black);
    }

    [Theory]
    [InlineData(PenColor.Red, 0)]
    [InlineData(PenColor.Blue, 1)]
    [InlineData(PenColor.Green, 2)]
    [InlineData(PenColor.Yellow, 3)]
    [InlineData(PenColor.White, 4)]
    [InlineData(PenColor.Magenta, 5)]
    [InlineData(PenColor.Orange, 6)]
    [InlineData(PenColor.Cyan, 7)]
    [InlineData(PenColor.Black, 8)]
    public void PenColor_ShouldHaveStableOrder(PenColor color, int expectedValue)
    {
        ((int)color).Should().Be(expectedValue);
    }
}

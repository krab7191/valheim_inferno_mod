using System;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Time;

public class ClockSettingTests
{
    [Theory]
    [InlineData(0, 0, "00:00")]
    [InlineData(6, 30, "06:30")]
    [InlineData(23, 59, "23:59")]
    [InlineData(24, 0, "00:00")]   // 24:00 is midnight
    [InlineData(10, 60, "11:00")]  // 60 minutes roll into the hour
    [InlineData(23, 60, "00:00")]
    [InlineData(24, 60, "01:00")]
    public void ToTimeOfDay_WrapsAroundTheClock(int hour, int minute, string expected) =>
        Assert.Equal(expected, ClockSetting.ToTimeOfDay(hour, minute).ToString());

    [Theory]
    [InlineData(-1)]
    [InlineData(25)]
    public void ToTimeOfDay_InvalidHour_Throws(int hour) =>
        Assert.Throws<ArgumentOutOfRangeException>("hour", () => ClockSetting.ToTimeOfDay(hour, 0));

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public void ToTimeOfDay_InvalidMinute_Throws(int minute) =>
        Assert.Throws<ArgumentOutOfRangeException>("minute", () => ClockSetting.ToTimeOfDay(0, minute));

    [Theory]
    [InlineData("0:00", "00:00")]
    [InlineData("6:30", "06:30")]
    [InlineData(" 18:00 ", "18:00")]
    [InlineData("24:00", "00:00")]
    [InlineData("10:60", "11:00")]
    public void TryParse_ValidText_UsesConfigRanges(string text, string expected)
    {
        Assert.True(ClockSetting.TryParse(text, out var time));
        Assert.Equal(expected, time.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("18")]
    [InlineData("1:2:3")]
    [InlineData(":00")]
    [InlineData("100:00")]
    [InlineData("18:0")]
    [InlineData("25:00")]
    [InlineData("18:61")]
    [InlineData("x8:00")]
    [InlineData("18:x0")]
    public void TryParse_InvalidText_FailsWithDefault(string? text)
    {
        Assert.False(ClockSetting.TryParse(text, out var time));
        Assert.Equal(default, time);
    }
}

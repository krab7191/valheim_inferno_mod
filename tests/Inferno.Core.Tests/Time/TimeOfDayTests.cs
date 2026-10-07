using System;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Time;

public class TimeOfDayTests
{
    [Theory]
    [InlineData(0, 0, 0, "00:00")]
    [InlineData(6, 5, 365, "06:05")]
    [InlineData(23, 59, 1439, "23:59")]
    public void FromHourMinute_ValidValues_SetsAllParts(int hour, int minute, int minutes, string text)
    {
        var time = TimeOfDay.FromHourMinute(hour, minute);

        Assert.Equal(hour, time.Hour);
        Assert.Equal(minute, time.Minute);
        Assert.Equal(minutes, time.MinutesSinceMidnight);
        Assert.Equal(text, time.ToString());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void FromHourMinute_InvalidHour_Throws(int hour) =>
        Assert.Throws<ArgumentOutOfRangeException>("hour", () => TimeOfDay.FromHourMinute(hour, 0));

    [Theory]
    [InlineData(-1)]
    [InlineData(60)]
    public void FromHourMinute_InvalidMinute_Throws(int minute) =>
        Assert.Throws<ArgumentOutOfRangeException>("minute", () => TimeOfDay.FromHourMinute(0, minute));

    [Theory]
    [InlineData(0.0, "00:00")]
    [InlineData(0.25, "06:00")]
    [InlineData(0.5, "12:00")]
    [InlineData(0.999, "23:58")]
    [InlineData(1.0, "00:00")]
    public void FromDayFraction_ValidValues_FloorsToMinute(double fraction, string expected) =>
        Assert.Equal(expected, TimeOfDay.FromDayFraction(fraction).ToString());

    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(double.NaN)]
    public void FromDayFraction_InvalidValues_Throws(double fraction) =>
        Assert.Throws<ArgumentOutOfRangeException>("fraction", () => TimeOfDay.FromDayFraction(fraction));

    [Theory]
    [InlineData("00:00", 0)]
    [InlineData("6:30", 390)]
    [InlineData("06:30", 390)]
    [InlineData(" 18:00 ", 1080)]
    [InlineData("23:59", 1439)]
    public void TryParse_ValidText_Succeeds(string text, int expectedMinutes)
    {
        Assert.True(TimeOfDay.TryParse(text, out var time));
        Assert.Equal(expectedMinutes, time.MinutesSinceMidnight);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("12:00:00")]
    [InlineData(":30")]
    [InlineData("123:00")]
    [InlineData("12:0")]
    [InlineData("12:000")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("-1:00")]
    [InlineData("1a:00")]
    [InlineData("12:3b")]
    [InlineData("１2:00")] // full-width digit
    public void TryParse_InvalidText_FailsWithDefault(string? text)
    {
        Assert.False(TimeOfDay.TryParse(text, out var time));
        Assert.Equal(default, time);
    }

    [Fact]
    public void Equality_ComparesMinutes()
    {
        var a = TimeOfDay.FromHourMinute(6, 0);
        var b = TimeOfDay.FromHourMinute(6, 0);
        var c = TimeOfDay.FromHourMinute(18, 0);

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object)"06:00"));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a != c);
        Assert.False(a == c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a.GetHashCode(), c.GetHashCode());
    }
}

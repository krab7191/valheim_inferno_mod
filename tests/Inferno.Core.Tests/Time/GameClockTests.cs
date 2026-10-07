using System;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Time;

public class GameClockTests
{
    private const long DayLength = 1800;

    [Theory]
    [InlineData(0, "00:00")]      // midnight
    [InlineData(135, "03:00")]    // half-way through the pre-dawn night segment
    [InlineData(270, "06:00")]    // raw 0.15 = sunrise
    [InlineData(900, "12:00")]    // raw 0.50 = noon
    [InlineData(1530, "18:00")]   // raw 0.85 = sunset
    [InlineData(1665, "21:00")]   // half-way through the evening night segment
    [InlineData(1800, "00:00")]   // next day wraps around
    [InlineData(2700, "12:00")]   // noon on day 2
    public void TimeAt_KnownWorldTimes_MatchVanillaClock(double worldTime, string expected) =>
        Assert.Equal(expected, GameClock.TimeAt(worldTime, DayLength).ToString());

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.075, 0.125)]
    [InlineData(0.15, 0.25)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.85, 0.75)]
    [InlineData(0.925, 0.875)]
    [InlineData(1.0, 1.0)]
    public void Rescale_MatchesEnvManRescaleDayFraction(double raw, double expected) =>
        Assert.Equal(expected, GameClock.Rescale(raw), precision: 12);

    [Fact]
    public void DisplayedDayFraction_IsMonotonicWithinOneDay()
    {
        var previous = -1.0;
        for (var t = 0; t < DayLength; t++)
        {
            var current = GameClock.DisplayedDayFraction(t, DayLength);
            Assert.True(current > previous, $"Not increasing at t={t}");
            previous = current;
        }
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DisplayedDayFraction_InvalidWorldTime_Throws(double worldTime) =>
        Assert.Throws<ArgumentOutOfRangeException>("worldTimeSeconds", () => GameClock.DisplayedDayFraction(worldTime, DayLength));

    [Theory]
    [InlineData(0)]
    [InlineData(-1800)]
    public void DisplayedDayFraction_InvalidDayLength_Throws(long dayLength) =>
        Assert.Throws<ArgumentOutOfRangeException>("dayLengthSeconds", () => GameClock.DisplayedDayFraction(0, dayLength));
}

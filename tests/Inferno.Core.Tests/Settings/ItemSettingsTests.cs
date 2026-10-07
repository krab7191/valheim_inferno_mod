using System;
using Inferno.Core.Settings;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Settings;

public class ItemSettingsTests
{
    private static readonly DailySchedule Night = new(TimeOfDay.FromHourMinute(18, 0), TimeOfDay.FromHourMinute(6, 0));

    [Fact]
    public void DefaultFor_LightSource_IsAlwaysOn()
    {
        var settings = ItemSettings.DefaultFor(ItemKind.LightSource);

        Assert.True(settings.AlwaysOn);
        Assert.Equal(BurnRate.Vanilla, settings.BurnRateLevel);
        Assert.True(settings.Schedule.IsAlwaysOn);
    }

    [Fact]
    public void DefaultFor_FuelStation_IsVanilla()
    {
        var settings = ItemSettings.DefaultFor(ItemKind.FuelStation);

        Assert.Same(ItemSettings.Vanilla, settings);
        Assert.False(settings.AlwaysOn);
        Assert.Equal(BurnRate.Vanilla, settings.BurnRateLevel);
        Assert.Equal(1.0, settings.BurnMultiplier);
        Assert.True(settings.Schedule.IsAlwaysOn);
    }

    [Theory]
    [InlineData(-11)]
    [InlineData(11)]
    public void Constructor_InvalidBurnRate_Throws(int level) =>
        Assert.Throws<ArgumentOutOfRangeException>("level", () => new ItemSettings(false, level, DailySchedule.AlwaysOn));

    [Fact]
    public void With_ChangesOnlyOneValue()
    {
        var start = ItemSettings.Vanilla;

        var alwaysOn = start.WithAlwaysOn(true);
        Assert.True(alwaysOn.AlwaysOn);
        Assert.Equal(start.BurnRateLevel, alwaysOn.BurnRateLevel);
        Assert.Equal(start.Schedule, alwaysOn.Schedule);

        var burn = start.WithBurnRate(-5);
        Assert.Equal(-5, burn.BurnRateLevel);
        Assert.Equal(0.5, burn.BurnMultiplier, precision: 12);
        Assert.False(burn.AlwaysOn);

        var scheduled = start.WithSchedule(Night);
        Assert.Equal(Night, scheduled.Schedule);
        Assert.Equal(start.BurnRateLevel, scheduled.BurnRateLevel);

        Assert.Throws<ArgumentOutOfRangeException>(() => start.WithBurnRate(20));

        Assert.True(start.Smoke);
        var noSmoke = start.WithSmoke(false);
        Assert.False(noSmoke.Smoke);
        Assert.Equal(start.Schedule, noSmoke.Schedule);
        Assert.False(noSmoke.WithAlwaysOn(true).WithBurnRate(1).WithSchedule(Night).Smoke);
    }

    [Fact]
    public void ToString_DescribesAllValues()
    {
        Assert.Equal("alwayson=on burnrate=0 schedule=always on smoke=on", ItemSettings.AlwaysOnDefault.ToString());
        Assert.Equal(
            "alwayson=off burnrate=-3 schedule=18:00-06:00 smoke=off",
            new ItemSettings(false, -3, Night, smoke: false).ToString());
    }

    [Fact]
    public void Equality_ComparesAllValues()
    {
        var a = new ItemSettings(true, 2, Night);
        var b = new ItemSettings(true, 2, Night);

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(null));
        Assert.False(a.Equals("settings"));
        Assert.False(a.Equals(a.WithAlwaysOn(false)));
        Assert.False(a.Equals(a.WithBurnRate(3)));
        Assert.False(a.Equals(a.WithSchedule(DailySchedule.AlwaysOn)));
        Assert.False(a.Equals(a.WithSmoke(false)));
        Assert.NotEqual(a.GetHashCode(), a.WithSmoke(false).GetHashCode());
        Assert.NotEqual(a.GetHashCode(), a.WithAlwaysOn(false).GetHashCode());
        Assert.NotEqual(a.GetHashCode(), a.WithBurnRate(3).GetHashCode());
        Assert.NotEqual(a.GetHashCode(), a.WithSchedule(DailySchedule.AlwaysOn).GetHashCode());
    }
}

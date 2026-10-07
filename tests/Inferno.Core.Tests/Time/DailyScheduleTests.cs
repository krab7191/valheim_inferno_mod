using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Time;

public class DailyScheduleTests
{
    private static TimeOfDay T(string text)
    {
        Assert.True(TimeOfDay.TryParse(text, out var time));
        return time;
    }

    private static DailySchedule S(string on, string off) => new(T(on), T(off));

    [Theory]
    [InlineData("00:00")]
    [InlineData("12:00")]
    [InlineData("23:59")]
    public void EqualOnAndOff_IsAlwaysOn(string now)
    {
        Assert.True(S("00:00", "00:00").IsOnAt(T(now)));
        Assert.True(S("18:00", "18:00").IsOnAt(T(now)));
    }

    [Theory]
    [InlineData("05:59", false)]
    [InlineData("06:00", true)]   // on time is inclusive
    [InlineData("12:00", true)]
    [InlineData("17:59", true)]
    [InlineData("18:00", false)]  // off time is exclusive
    [InlineData("23:00", false)]
    public void DaytimeWindow(string now, bool expected) =>
        Assert.Equal(expected, S("06:00", "18:00").IsOnAt(T(now)));

    [Theory]
    [InlineData("17:59", false)]
    [InlineData("18:00", true)]
    [InlineData("23:59", true)]
    [InlineData("00:00", true)]
    [InlineData("05:59", true)]
    [InlineData("06:00", false)]
    [InlineData("12:00", false)]
    public void WindowAcrossMidnight(string now, bool expected) =>
        Assert.Equal(expected, S("18:00", "06:00").IsOnAt(T(now)));

    [Fact]
    public void AlwaysOn_IsDefaultMidnightToMidnight()
    {
        var schedule = DailySchedule.AlwaysOn;

        Assert.True(schedule.IsAlwaysOn);
        Assert.Equal(S("00:00", "00:00"), schedule);
        Assert.Equal("always on", schedule.ToString());
    }

    [Fact]
    public void ToString_ShowsWindow()
    {
        Assert.False(S("18:00", "06:00").IsAlwaysOn);
        Assert.Equal("18:00-06:00", S("18:00", "06:00").ToString());
    }

    [Fact]
    public void Equality_ComparesBothTimes()
    {
        var a = S("18:00", "06:00");
        var b = S("18:00", "06:00");
        var differentOff = S("18:00", "07:00");
        var differentOn = S("19:00", "06:00");

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object)"18:00-06:00"));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a != differentOff);
        Assert.True(a != differentOn);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a.GetHashCode(), differentOff.GetHashCode());
        Assert.NotEqual(a.GetHashCode(), differentOn.GetHashCode());
    }
}

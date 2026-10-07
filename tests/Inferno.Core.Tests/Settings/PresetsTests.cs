using Inferno.Core.Settings;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Settings;

public class PresetsTests
{
    [Theory]
    [InlineData("eternal")]
    [InlineData("NIGHT")]
    [InlineData("Vanilla")]
    public void TryGet_FindsByNameIgnoringCase(string name)
    {
        Assert.True(Presets.TryGet(name, out var preset));
        Assert.Equal(name.ToLowerInvariant(), preset.Name);
    }

    [Theory]
    [InlineData("party")]
    [InlineData(null)]
    public void TryGet_Unknown_ReturnsFalse(string? name)
    {
        Assert.False(Presets.TryGet(name, out var preset));
        Assert.Null(preset);
    }

    [Fact]
    public void DefaultTargets()
    {
        Assert.Equal("lights", Presets.Eternal.DefaultTarget);
        Assert.Equal("lights", Presets.Night.DefaultTarget);
        Assert.Equal("all", Presets.Vanilla.DefaultTarget);
        Assert.Equal(3, Presets.All.Count);
    }

    [Fact]
    public void Night_ItemWithoutSwitch_IsSkipped() =>
        Assert.Null(Presets.Night.Apply(TestData.Smelter, ItemSettings.Vanilla));

    [Fact]
    public void Apply_KeepsSmoke()
    {
        var noSmoke = ItemSettings.Vanilla.WithSmoke(false);

        Assert.False(Presets.Eternal.Apply(TestData.Torch, noSmoke)!.Smoke);
        Assert.False(Presets.Night.Apply(TestData.Torch, noSmoke)!.Smoke);
        Assert.False(Presets.Vanilla.Apply(TestData.Torch, noSmoke)!.Smoke);
    }

    [Fact]
    public void Schedules()
    {
        Assert.Equal("18:00-06:00", DailySchedule.Night.ToString());
        Assert.Equal("06:00-18:00", DailySchedule.Day.ToString());
    }
}

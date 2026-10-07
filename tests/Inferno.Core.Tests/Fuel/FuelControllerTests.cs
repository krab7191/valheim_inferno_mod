using System;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;
using Inferno.Core.Time;
using Xunit;

namespace Inferno.Core.Tests.Fuel;

public class FuelControllerTests
{
    private const float Max = 10f;

    private const float SecPerFuel = 100f;

    private static readonly FuelItemInfo Fireplace = new(Max, hasOnOffSwitch: true, secondsPerFuel: SecPerFuel);
    private static readonly FuelItemInfo Smelter = new(Max, hasOnOffSwitch: false);
    private static readonly DailySchedule Night = new(TimeOfDay.FromHourMinute(18, 0), TimeOfDay.FromHourMinute(6, 0));
    private static readonly TimeOfDay Noon = TimeOfDay.FromHourMinute(12, 0);
    private static readonly TimeOfDay Midnight = TimeOfDay.FromHourMinute(0, 0);

    private static FuelState State(float fuel, bool on = true, bool offByInferno = false, bool unattended = false, double seconds = 0) =>
        new(fuel, on, offByInferno, unattended, seconds);

    private static FuelDecision Decide(
        ItemSettings settings,
        FuelItemInfo item,
        FuelState state,
        float? baseline = null,
        TimeOfDay now = default,
        bool ignoreRain = false) =>
        FuelController.Decide(settings, item, state, baseline, now, ignoreRain);

    // ---- Argument checks ----

    [Fact]
    public void Decide_NullSettings_Throws() =>
        Assert.Throws<ArgumentNullException>("settings", () => FuelController.Decide(null!, Fireplace, State(1), null, Noon, false));

    [Fact]
    public void Decide_NullItem_Throws() =>
        Assert.Throws<ArgumentNullException>("item", () => FuelController.Decide(ItemSettings.Vanilla, null!, State(1), null, Noon, false));

    // ---- Always on ----

    [Theory]
    [InlineData(0f)]
    [InlineData(4.5f)]
    [InlineData(9.5f)]       // half a unit below full
    [InlineData(-1f)]        // corrupt
    [InlineData(float.NaN)]  // corrupt
    public void AlwaysOn_NotFull_FillsToMax(float fuel)
    {
        var decision = Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(fuel));

        Assert.Equal(Max, decision.SetFuel);
        Assert.Equal(Max, decision.Baseline);
        Assert.True(decision.HasChanges);
    }

    [Theory]
    [InlineData(10f)]
    [InlineData(9.51f)]     // still shows as 10/10 in game (rounded up)
    [InlineData(11f)]       // over max (e.g. prefab changed by a patch): leave alone
    public void AlwaysOn_NearlyFull_WritesNothing(float fuel)
    {
        var decision = Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(fuel));

        Assert.Null(decision.SetFuel);
        Assert.Equal(fuel, decision.Baseline);
        Assert.False(decision.HasChanges);
    }

    [Theory]
    [InlineData(1f, 0.5f, true)]    // small capacity: margin is half the capacity
    [InlineData(1f, 0.51f, false)]
    public void AlwaysOn_SmallCapacity_UsesHalfCapacityMargin(float max, float fuel, bool refills)
    {
        var item = new FuelItemInfo(max, hasOnOffSwitch: true);

        Assert.Equal(refills, Decide(ItemSettings.AlwaysOnDefault, item, State(fuel)).SetFuel.HasValue);
    }

    [Fact]
    public void AlwaysOn_OverridesSchedule_DuringOffWindow()
    {
        var settings = ItemSettings.AlwaysOnDefault.WithSchedule(Night);

        var decision = Decide(settings, Fireplace, State(Max), now: Noon);

        Assert.Null(decision.SetSwitchedOn);
        Assert.Null(decision.SetSwitchedOffByInferno);
    }

    [Fact]
    public void AlwaysOn_SwitchesBackOnWhatInfernoSwitchedOff()
    {
        // e.g. the schedule switched it off, then the user enabled AlwaysOn.
        var decision = Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max, on: false, offByInferno: true), now: Noon);

        Assert.Equal(true, decision.SetSwitchedOn);
        Assert.Equal(false, decision.SetSwitchedOffByInferno);
    }

    [Fact]
    public void AlwaysOn_LeavesPlayerSwitchedOffItemsOff()
    {
        var decision = Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max, on: false, offByInferno: false));

        Assert.Null(decision.SetSwitchedOn);
        Assert.Null(decision.SetSwitchedOffByInferno);
    }

    [Fact]
    public void AlwaysOn_Unattended_RefreshesBurnClockAfterStep()
    {
        Assert.True(Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max, unattended: true, seconds: 30)).RefreshBurnClock);
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max, unattended: true, seconds: 29)).RefreshBurnClock);
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max, unattended: false, seconds: 300)).RefreshBurnClock);
    }

    [Fact]
    public void RefreshBurnClock_OnlyForItemsThatBurnOverTime()
    {
        // Smelters and ovens keep time differently; the burn clock is a fireplace concept.
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, Smelter, State(Max, unattended: true, seconds: 300)).RefreshBurnClock);
        var noTimeBurn = new FuelItemInfo(Max, hasOnOffSwitch: true, secondsPerFuel: 0f);
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, noTimeBurn, State(Max, unattended: true, seconds: 300)).RefreshBurnClock);
    }

    // ---- Nobody nearby: server burns the fuel ----

    [Theory]
    [InlineData(0, 300.0, 7f)]     // vanilla: 300 s / 100 s per fuel = 3
    [InlineData(-5, 300.0, 8.5f)]  // half
    [InlineData(10, 300.0, 4f)]    // double
    [InlineData(0, 5000.0, 0f)]    // runs dry, never negative
    public void Unattended_BurnsFuelOnServer(int burnRate, double seconds, float expected)
    {
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(burnRate), Fireplace, State(10f, unattended: true, seconds: seconds), baseline: 10f);

        Assert.Equal(expected, decision.SetFuel!.Value, precision: 4);
        Assert.True(decision.RefreshBurnClock);
        Assert.Equal(expected, decision.Baseline, precision: 4);
        Assert.False(decision.MustWin);
    }

    [Fact]
    public void Unattended_NoFuelBurnedAtMinusTen()
    {
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(-10), Fireplace, State(10f, unattended: true, seconds: 300), baseline: 10f);

        Assert.Null(decision.SetFuel);
        Assert.True(decision.RefreshBurnClock);
    }

    [Fact]
    public void Unattended_SwitchedOff_KeepsFuelButMovesClock()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        var decision = Decide(settings, Fireplace, State(5f, on: false, offByInferno: true, unattended: true, seconds: 600), baseline: 5f, now: Noon);

        Assert.Null(decision.SetFuel);
        Assert.True(decision.RefreshBurnClock);
    }

    [Fact]
    public void Unattended_Empty_NothingToBurn()
    {
        var decision = Decide(ItemSettings.Vanilla, Fireplace, State(0f, unattended: true, seconds: 600), baseline: 0f);

        Assert.Null(decision.SetFuel);
        Assert.True(decision.RefreshBurnClock);
    }

    [Fact]
    public void Unattended_BeforeStep_DoesNothing() =>
        Assert.False(Decide(ItemSettings.Vanilla, Fireplace, State(5f, unattended: true, seconds: 10), baseline: 5f).HasChanges);

    [Fact]
    public void Unattended_OwnerJustLeft_CorrectsThenBurns()
    {
        // Owner burned 2 (8 -> 6) at half rate: corrected to 7; then 200 s unattended at half rate burns 1 more.
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(-5), Fireplace, State(6f, unattended: true, seconds: 200), baseline: 8f);

        Assert.Equal(6f, decision.SetFuel!.Value, precision: 4);
    }

    [Fact]
    public void Attended_NeverBurnsOnServer()
    {
        // The owning player burns the fuel; the server only corrects.
        Assert.False(Decide(ItemSettings.Vanilla, Fireplace, State(5f, unattended: false, seconds: 600), baseline: 5f).HasChanges);
    }

    // ---- Write precedence ----

    [Fact]
    public void MustWin_ForRefillsAndSwitchesOnOwnedItems()
    {
        Assert.True(Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(2f)).MustWin);
        Assert.True(Decide(ItemSettings.Vanilla.WithSchedule(Night), Fireplace, State(5f), now: Noon).MustWin);
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(2f, unattended: true)).MustWin);
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max)).MustWin);
        Assert.False(Decide(ItemSettings.Vanilla.WithBurnRate(-5), Fireplace, State(6f), baseline: 8f).MustWin);
    }

    [Fact]
    public void AlwaysOn_Smelter_FillsWithoutTouchingSwitch()
    {
        var decision = Decide(ItemSettings.AlwaysOnDefault, Smelter, State(2f));

        Assert.Equal(Max, decision.SetFuel);
        Assert.Null(decision.SetSwitchedOn);
        Assert.Null(decision.SetSwitchedOffByInferno);
    }

    // ---- Burn rate ----

    [Fact]
    public void BurnRate_FirstScan_OnlyRecordsBaseline()
    {
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(-5), Fireplace, State(6f), baseline: null);

        Assert.Null(decision.SetFuel);
        Assert.Equal(6f, decision.Baseline);
    }

    [Theory]
    [InlineData(0, 8f, 6f, null)]     // vanilla: no correction
    [InlineData(-5, 8f, 6f, 7f)]      // half speed: give back half of the 2 burned
    [InlineData(-10, 8f, 6f, 8f)]     // no burn: give it all back
    [InlineData(5, 8f, 6f, 5f)]       // 1.5x: take 1 extra
    [InlineData(10, 8f, 6f, 4f)]      // 2x: take 2 extra
    [InlineData(10, 1f, 0.5f, 0f)]    // never below zero
    [InlineData(-10, 10f, 9f, 10f)]   // never above max
    public void BurnRate_ScalesFuelLostSinceLastScan(int level, float baseline, float fuel, float? expected)
    {
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(level), Fireplace, State(fuel), baseline);

        Assert.Equal(expected, decision.SetFuel);
        Assert.Equal(expected ?? fuel, decision.Baseline);
    }

    [Fact]
    public void BurnRate_FireplaceAlreadyEmptyAtFastRate_WritesNothing()
    {
        // 2x burn of an item that was at 0.3 and is now 0: correction would also be 0.
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(10), Fireplace, State(0f), baseline: 0.3f);

        Assert.Null(decision.SetFuel);
        Assert.Equal(0f, decision.Baseline);
    }

    [Theory]
    [InlineData(4f, 6f)]  // player added fuel
    [InlineData(6f, 6f)]  // nothing burned
    public void BurnRate_FuelNotDecreased_RebasesWithoutWriting(float baseline, float fuel)
    {
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(-5), Fireplace, State(fuel), baseline);

        Assert.Null(decision.SetFuel);
        Assert.Equal(fuel, decision.Baseline);
    }

    [Fact]
    public void BurnRate_AppliesToSmelters()
    {
        var decision = Decide(ItemSettings.Vanilla.WithBurnRate(-5), Smelter, State(6f), baseline: 8f);

        Assert.Equal(7f, decision.SetFuel);
    }

    // ---- Schedule ----

    [Fact]
    public void Schedule_OutsideWindow_SwitchesOffAndMarks_KeepsFuel()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        var decision = Decide(settings, Fireplace, State(5f), baseline: 5f, now: Noon);

        Assert.Equal(false, decision.SetSwitchedOn);
        Assert.Equal(true, decision.SetSwitchedOffByInferno);
        Assert.Null(decision.SetFuel);
        Assert.Equal(5f, decision.Baseline);
    }

    [Fact]
    public void Schedule_OutsideWindow_AlreadyOff_WritesNothing()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        var byInferno = Decide(settings, Fireplace, State(5f, on: false, offByInferno: true), now: Noon);
        var byPlayer = Decide(settings, Fireplace, State(5f, on: false, offByInferno: false), now: Noon);

        Assert.False(byInferno.HasChanges);
        Assert.False(byPlayer.HasChanges);
    }

    [Fact]
    public void Schedule_InsideWindow_SwitchesBackOnWhatInfernoSwitchedOff()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        var decision = Decide(settings, Fireplace, State(5f, on: false, offByInferno: true), now: Midnight);

        Assert.Equal(true, decision.SetSwitchedOn);
        Assert.Equal(false, decision.SetSwitchedOffByInferno);
    }

    [Fact]
    public void Schedule_InsideWindow_LeavesPlayerSwitchedOffItemsOff()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        var decision = Decide(settings, Fireplace, State(5f, on: false, offByInferno: false), now: Midnight);

        Assert.False(decision.HasChanges);
    }

    [Fact]
    public void Schedule_InsideWindow_PlayerAlreadySwitchedOn_ClearsStaleMarker()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        var decision = Decide(settings, Fireplace, State(5f, on: true, offByInferno: true), now: Midnight);

        Assert.Null(decision.SetSwitchedOn);
        Assert.Equal(false, decision.SetSwitchedOffByInferno);
    }

    [Fact]
    public void Schedule_InsideWindow_OnAndUnmarked_WritesNothing()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        Assert.False(Decide(settings, Fireplace, State(5f), now: Midnight).HasChanges);
    }

    [Fact]
    public void Schedule_IgnoredForItemsWithoutSwitch()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        Assert.False(Decide(settings, Smelter, State(5f), now: Noon).HasChanges);
    }

    [Fact]
    public void Schedule_DuringOffWindow_StillCorrectsBurnRate()
    {
        // Vanilla doesn't burn while switched off, but if fuel was lost (e.g. the scan just before switch-off) it is still scaled.
        var settings = ItemSettings.Vanilla.WithSchedule(Night).WithBurnRate(-10);

        var decision = Decide(settings, Fireplace, State(4f, on: false, offByInferno: true), baseline: 5f, now: Noon);

        Assert.Equal(5f, decision.SetFuel);
    }

    // ---- Ignore rain ----

    [Fact]
    public void IgnoreRain_AlwaysOn_SwitchesBackOnUnmarkedOffLights()
    {
        var decision = Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max, on: false), ignoreRain: true);

        Assert.Equal(true, decision.SetSwitchedOn);
        Assert.Null(decision.SetSwitchedOffByInferno);
    }

    [Fact]
    public void IgnoreRain_InsideScheduleWindow_SwitchesBackOn()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        var decision = Decide(settings, Fireplace, State(5f, on: false), now: Midnight, ignoreRain: true);

        Assert.Equal(true, decision.SetSwitchedOn);
    }

    [Fact]
    public void IgnoreRain_OutsideScheduleWindow_StaysOff()
    {
        var settings = ItemSettings.Vanilla.WithSchedule(Night);

        Assert.False(Decide(settings, Fireplace, State(5f, on: false), now: Noon, ignoreRain: true).HasChanges);
    }

    [Fact]
    public void IgnoreRain_LightAlreadyOn_WritesNothing() =>
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, Fireplace, State(Max), ignoreRain: true).HasChanges);

    [Fact]
    public void IgnoreRain_NoSwitch_WritesNothing() =>
        Assert.False(Decide(ItemSettings.AlwaysOnDefault, Smelter, State(Max), ignoreRain: true).HasChanges);

    // ---- Shutdown restore ----

    [Fact]
    public void RestoreForShutdown_MarkedAndOff_SwitchesOnAndClearsMarker()
    {
        var decision = FuelController.RestoreForShutdown(State(5f, on: false, offByInferno: true));

        Assert.Equal(true, decision.SetSwitchedOn);
        Assert.Equal(false, decision.SetSwitchedOffByInferno);
        Assert.Null(decision.SetFuel);
        Assert.Equal(5f, decision.Baseline);
    }

    [Fact]
    public void RestoreForShutdown_MarkedButOn_OnlyClearsMarker()
    {
        var decision = FuelController.RestoreForShutdown(State(5f, on: true, offByInferno: true));

        Assert.Null(decision.SetSwitchedOn);
        Assert.Equal(false, decision.SetSwitchedOffByInferno);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RestoreForShutdown_NotMarked_WritesNothing(bool on)
    {
        var decision = FuelController.RestoreForShutdown(State(5f, on: on));

        Assert.False(decision.HasChanges);
        Assert.Equal(5f, decision.Baseline);
    }

    // ---- FuelItemInfo ----

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void FuelItemInfo_InvalidMaxFuel_Throws(float maxFuel) =>
        Assert.Throws<ArgumentOutOfRangeException>("maxFuel", () => new FuelItemInfo(maxFuel, true));

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void FuelItemInfo_InvalidSecondsPerFuel_Throws(float seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>("secondsPerFuel", () => new FuelItemInfo(1f, true, seconds));

    [Fact]
    public void FuelItemInfo_StoresValues()
    {
        var info = new FuelItemInfo(6f, false);

        Assert.Equal(6f, info.MaxFuel);
        Assert.False(info.HasOnOffSwitch);
        Assert.Equal(0f, info.SecondsPerFuel);
        Assert.False(info.BurnsOverTime);
        Assert.True(Fireplace.BurnsOverTime);
        Assert.False(new FuelItemInfo(6f, false, 100f).BurnsOverTime);
    }

    [Fact]
    public void FuelState_StoresValues()
    {
        var state = new FuelState(3f, isSwitchedOn: false, switchedOffByInferno: true, isUnattended: true, secondsSinceBurnClock: 42);

        Assert.Equal(42, state.SecondsSinceBurnClock);
        Assert.Equal(3f, state.Fuel);
        Assert.False(state.IsSwitchedOn);
        Assert.True(state.SwitchedOffByInferno);
        Assert.True(state.IsUnattended);
    }
}

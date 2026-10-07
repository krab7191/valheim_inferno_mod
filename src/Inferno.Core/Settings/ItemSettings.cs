using System;
using Inferno.Core.Time;

namespace Inferno.Core.Settings;

/// <summary>The settings for one item (prefab). Immutable; validated on construction.</summary>
public sealed class ItemSettings : IEquatable<ItemSettings>
{
    /// <summary>Creates validated settings.</summary>
    /// <param name="alwaysOn">Keep fuel full at all times. Overrides the schedule.</param>
    /// <param name="burnRate">Burn rate level, -10 to +10 (see <see cref="BurnRate"/>).</param>
    /// <param name="schedule">Daily on/off window; ignored when <paramref name="alwaysOn"/> is set.</param>
    /// <param name="smoke">Whether the item makes smoke. Only players with the Inferno client mod see it removed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="burnRate"/> is out of range.</exception>
    public ItemSettings(bool alwaysOn, int burnRate, DailySchedule schedule, bool smoke = true)
    {
        // Validates the range; the multiplier itself is derived on demand.
        _ = BurnRate.ToMultiplier(burnRate);

        AlwaysOn = alwaysOn;
        BurnRateLevel = burnRate;
        Schedule = schedule;
        Smoke = smoke;
    }

    /// <summary>Plain vanilla behaviour.</summary>
    public static ItemSettings Vanilla { get; } = new(alwaysOn: false, BurnRate.Vanilla, DailySchedule.AlwaysOn);

    /// <summary>Never needs fuel; the default for light sources.</summary>
    public static ItemSettings AlwaysOnDefault { get; } = new(alwaysOn: true, BurnRate.Vanilla, DailySchedule.AlwaysOn);

    /// <summary>Keep fuel full at all times. Overrides the schedule.</summary>
    public bool AlwaysOn { get; }

    /// <summary>Burn rate level, -10 to +10.</summary>
    public int BurnRateLevel { get; }

    /// <summary>Fuel-use multiplier derived from <see cref="BurnRateLevel"/>.</summary>
    public double BurnMultiplier => BurnRate.ToMultiplier(BurnRateLevel);

    /// <summary>Daily on/off window.</summary>
    public DailySchedule Schedule { get; }

    /// <summary>Whether the item makes smoke (applied by the Inferno client mod only).</summary>
    public bool Smoke { get; }

    /// <summary>Default settings for an item kind: light sources are always on, everything else is vanilla.</summary>
    public static ItemSettings DefaultFor(ItemKind kind) => kind == ItemKind.LightSource ? AlwaysOnDefault : Vanilla;

    /// <summary>Returns a copy with <see cref="AlwaysOn"/> changed.</summary>
    public ItemSettings WithAlwaysOn(bool value) => new(value, BurnRateLevel, Schedule, Smoke);

    /// <summary>Returns a copy with <see cref="BurnRateLevel"/> changed.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The level is out of range.</exception>
    public ItemSettings WithBurnRate(int value) => new(AlwaysOn, value, Schedule, Smoke);

    /// <summary>Returns a copy with <see cref="Schedule"/> changed.</summary>
    public ItemSettings WithSchedule(DailySchedule value) => new(AlwaysOn, BurnRateLevel, value, Smoke);

    /// <summary>Returns a copy with <see cref="Smoke"/> changed.</summary>
    public ItemSettings WithSmoke(bool value) => new(AlwaysOn, BurnRateLevel, Schedule, value);

    /// <inheritdoc />
    public override string ToString() =>
        $"alwayson={OnOff(AlwaysOn)} burnrate={BurnRateLevel} schedule={Schedule} smoke={OnOff(Smoke)}";

    /// <inheritdoc />
    public bool Equals(ItemSettings? other) =>
        other is not null
        && AlwaysOn == other.AlwaysOn
        && BurnRateLevel == other.BurnRateLevel
        && Schedule == other.Schedule
        && Smoke == other.Smoke;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ItemSettings);

    /// <inheritdoc />
    public override int GetHashCode() =>
        (Schedule.GetHashCode() * 128) + ((BurnRateLevel - BurnRate.Min) * 4) + (AlwaysOn ? 2 : 0) + (Smoke ? 1 : 0);

    private static string OnOff(bool value) => value ? "on" : "off";
}

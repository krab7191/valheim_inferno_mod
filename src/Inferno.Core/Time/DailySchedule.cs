using System;

namespace Inferno.Core.Time;

/// <summary>
/// One on/off window per in-game day. When the on and off times are equal there is no window: always on.
/// </summary>
public readonly struct DailySchedule : IEquatable<DailySchedule>
{
    /// <summary>Creates a schedule that turns on at <paramref name="onTime"/> and off at <paramref name="offTime"/>.</summary>
    public DailySchedule(TimeOfDay onTime, TimeOfDay offTime)
    {
        OnTime = onTime;
        OffTime = offTime;
    }

    /// <summary>A schedule that is always on (on = off = 00:00), the default.</summary>
    public static DailySchedule AlwaysOn => default;

    /// <summary>Time the light turns on.</summary>
    public TimeOfDay OnTime { get; }

    /// <summary>Time the light turns off.</summary>
    public TimeOfDay OffTime { get; }

    /// <summary>True when on and off times are equal, meaning the schedule never turns anything off.</summary>
    public bool IsAlwaysOn => OnTime == OffTime;

    /// <summary>
    /// Whether the light should be on at <paramref name="now"/>. The on time is inclusive and the off time
    /// exclusive; windows may cross midnight (e.g. 18:00 to 06:00).
    /// </summary>
    public bool IsOnAt(TimeOfDay now)
    {
        var on = OnTime.MinutesSinceMidnight;
        var off = OffTime.MinutesSinceMidnight;
        var t = now.MinutesSinceMidnight;

        if (on == off)
        {
            return true;
        }

        return on < off
            ? t >= on && t < off
            : t >= on || t < off;
    }

    /// <inheritdoc />
    public override string ToString() => IsAlwaysOn ? "always on" : $"{OnTime}-{OffTime}";

    /// <inheritdoc />
    public bool Equals(DailySchedule other) => OnTime == other.OnTime && OffTime == other.OffTime;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DailySchedule other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => (OnTime.MinutesSinceMidnight * TimeOfDay.MinutesPerDay) + OffTime.MinutesSinceMidnight;

    /// <summary>Equality.</summary>
    public static bool operator ==(DailySchedule left, DailySchedule right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(DailySchedule left, DailySchedule right) => !left.Equals(right);
}

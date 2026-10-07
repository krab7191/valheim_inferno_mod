using System;
using System.Globalization;

namespace Inferno.Core.Time;

/// <summary>A time on the in-game 24-hour clock, with minute precision (00:00 to 23:59).</summary>
public readonly struct TimeOfDay : IEquatable<TimeOfDay>
{
    /// <summary>Minutes in one day.</summary>
    public const int MinutesPerDay = 24 * 60;

    private TimeOfDay(int minutesSinceMidnight) => MinutesSinceMidnight = minutesSinceMidnight;

    /// <summary>Minutes since midnight, 0 to 1439.</summary>
    public int MinutesSinceMidnight { get; }

    /// <summary>Hour, 0 to 23.</summary>
    public int Hour => MinutesSinceMidnight / 60;

    /// <summary>Minute, 0 to 59.</summary>
    public int Minute => MinutesSinceMidnight % 60;

    /// <summary>Creates a time from an hour (0 to 23) and a minute (0 to 59).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The hour or minute is out of range.</exception>
    public static TimeOfDay FromHourMinute(int hour, int minute)
    {
        if (hour is < 0 or > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "Hour must be 0 to 23.");
        }

        if (minute is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "Minute must be 0 to 59.");
        }

        return new TimeOfDay((hour * 60) + minute);
    }

    /// <summary>
    /// Converts a day fraction (0 = midnight, 0.25 = 06:00, 0.5 = noon) to a time, rounding down to the minute.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The fraction is not in [0, 1] or is NaN.</exception>
    public static TimeOfDay FromDayFraction(double fraction)
    {
        if (!(fraction >= 0.0 && fraction <= 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "Day fraction must be 0 to 1.");
        }

        // A fraction of exactly 1 is the end of the day, i.e. the next midnight.
        var minutes = (int)Math.Floor(fraction * MinutesPerDay) % MinutesPerDay;
        return new TimeOfDay(minutes);
    }

    /// <summary>
    /// Parses "H:MM" or "HH:MM" (24-hour clock). Surrounding whitespace is ignored.
    /// </summary>
    public static bool TryParse(string? text, out TimeOfDay result)
    {
        result = default;
        if (text is null)
        {
            return false;
        }

        var parts = text.Trim().Split(':');
        if (parts.Length != 2 || parts[0].Length is < 1 or > 2 || parts[1].Length != 2)
        {
            return false;
        }

        if (!TryParseDigits(parts[0], out var hour) || !TryParseDigits(parts[1], out var minute))
        {
            return false;
        }

        if (hour > 23 || minute > 59)
        {
            return false;
        }

        result = new TimeOfDay((hour * 60) + minute);
        return true;
    }

    /// <summary>Formats as "HH:MM".</summary>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", Hour, Minute);

    /// <inheritdoc />
    public bool Equals(TimeOfDay other) => MinutesSinceMidnight == other.MinutesSinceMidnight;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is TimeOfDay other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => MinutesSinceMidnight;

    /// <summary>Equality.</summary>
    public static bool operator ==(TimeOfDay left, TimeOfDay right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(TimeOfDay left, TimeOfDay right) => !left.Equals(right);

    // int.TryParse would also accept signs and Unicode digits; only ASCII digits are valid here.
    internal static bool TryParseDigits(string text, out int value)
    {
        value = 0;
        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }

            value = (value * 10) + (c - '0');
        }

        return true;
    }
}

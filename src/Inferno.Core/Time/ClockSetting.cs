using System;

namespace Inferno.Core.Time;

/// <summary>
/// Turns the hour and minute values a user enters (hours 0 to 24, minutes 0 to 60) into a <see cref="TimeOfDay"/>.
/// </summary>
/// <remarks>
/// The upper bounds are inclusive so users can write 24:00 for midnight or 10:60 for 11:00; values wrap around
/// the 24-hour clock (24:00 = 00:00, 24:60 = 01:00).
/// </remarks>
public static class ClockSetting
{
    /// <summary>Largest hour a user may enter.</summary>
    public const int MaxHour = 24;

    /// <summary>Largest minute a user may enter.</summary>
    public const int MaxMinute = 60;

    /// <summary>Converts user-entered hour and minute to a time of day.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The hour is outside 0 to 24 or the minute outside 0 to 60.</exception>
    public static TimeOfDay ToTimeOfDay(int hour, int minute)
    {
        if (hour is < 0 or > MaxHour)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "Hour must be 0 to 24.");
        }

        if (minute is < 0 or > MaxMinute)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "Minute must be 0 to 60.");
        }

        var minutes = ((hour * 60) + minute) % TimeOfDay.MinutesPerDay;
        return TimeOfDay.FromHourMinute(minutes / 60, minutes % 60);
    }

    /// <summary>
    /// Parses "H:MM" or "HH:MM" with the same ranges as the config (hours 0 to 24, minutes 0 to 60), so
    /// "24:00" is accepted as midnight. Surrounding whitespace is ignored.
    /// </summary>
    public static bool TryParse(string? text, out TimeOfDay result)
    {
        result = default;
        var parts = text?.Trim().Split(':');
        if (parts is not { Length: 2 } || parts[0].Length is < 1 or > 2 || parts[1].Length != 2)
        {
            return false;
        }

        if (!TimeOfDay.TryParseDigits(parts[0], out var hour)
            || !TimeOfDay.TryParseDigits(parts[1], out var minute)
            || hour > MaxHour
            || minute > MaxMinute)
        {
            return false;
        }

        result = ToTimeOfDay(hour, minute);
        return true;
    }
}

using System;

namespace Inferno.Core.Time;

/// <summary>
/// Converts world time to the in-game clock exactly the way Valheim does (EnvMan, game version 1.0.17).
/// </summary>
/// <remarks>
/// Valheim stretches daylight: the raw fraction of the day is rescaled so that sunrise (raw 0.15) shows as
/// 06:00 and sunset (raw 0.85) as 18:00. Daytime is 70 % of real day length, night 30 %.
/// Source: <c>EnvMan.RescaleDayFraction</c> and <c>EnvMan.FixedUpdate</c>.
/// </remarks>
public static class GameClock
{
    private const double RawSunrise = 0.15;
    private const double RawSunset = 0.85;

    /// <summary>Returns the in-game time of day for a world time.</summary>
    /// <param name="worldTimeSeconds">World time in seconds (<c>ZNet.GetTimeSeconds()</c>); must be ≥ 0.</param>
    /// <param name="dayLengthSeconds">Length of one in-game day in seconds (<c>EnvMan.m_dayLengthSec</c>); must be &gt; 0.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of range or NaN.</exception>
    public static TimeOfDay TimeAt(double worldTimeSeconds, long dayLengthSeconds) =>
        TimeOfDay.FromDayFraction(DisplayedDayFraction(worldTimeSeconds, dayLengthSeconds));

    /// <summary>
    /// Returns the rescaled day fraction (0 = midnight, 0.25 = sunrise/06:00, 0.75 = sunset/18:00).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of range or NaN.</exception>
    public static double DisplayedDayFraction(double worldTimeSeconds, long dayLengthSeconds)
    {
        if (!(worldTimeSeconds >= 0.0) || double.IsInfinity(worldTimeSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(worldTimeSeconds), worldTimeSeconds, "World time must be a finite value ≥ 0.");
        }

        if (dayLengthSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dayLengthSeconds), dayLengthSeconds, "Day length must be > 0.");
        }

        var raw = worldTimeSeconds % dayLengthSeconds / dayLengthSeconds;
        return Rescale(raw);
    }

    /// <summary>Port of <c>EnvMan.RescaleDayFraction</c>.</summary>
    internal static double Rescale(double raw)
    {
        if (raw >= RawSunrise && raw <= RawSunset)
        {
            return 0.25 + ((raw - RawSunrise) / (RawSunset - RawSunrise) * 0.5);
        }

        if (raw < 0.5)
        {
            return raw / RawSunrise * 0.25;
        }

        return 0.75 + ((raw - RawSunset) / (1.0 - RawSunset) * 0.25);
    }
}

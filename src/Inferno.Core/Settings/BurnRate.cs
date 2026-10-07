using System;

namespace Inferno.Core.Settings;

/// <summary>
/// Fuel burn rate relative to vanilla, as a whole-number level from -10 to +10.
/// Each step is 10 %: 0 = vanilla, +10 = burns twice as fast, -10 = burns no fuel at all.
/// </summary>
public static class BurnRate
{
    /// <summary>Slowest level: no fuel is burned.</summary>
    public const int Min = -10;

    /// <summary>Fastest level: fuel burns twice as fast as vanilla.</summary>
    public const int Max = 10;

    /// <summary>Vanilla burn rate.</summary>
    public const int Vanilla = 0;

    /// <summary>Returns the fuel-use multiplier for a level (0.0 to 2.0).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The level is outside -10 to +10.</exception>
    public static double ToMultiplier(int level)
    {
        if (level is < Min or > Max)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Burn rate must be -10 to 10.");
        }

        return 1.0 + (level / 10.0);
    }
}

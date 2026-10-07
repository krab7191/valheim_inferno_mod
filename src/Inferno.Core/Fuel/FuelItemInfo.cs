using System;

namespace Inferno.Core.Fuel;

/// <summary>Fixed facts about an item type, read from its prefab.</summary>
public sealed class FuelItemInfo
{
    /// <summary>Creates item info.</summary>
    /// <param name="maxFuel">Fuel capacity (<c>m_maxFuel</c>); must be &gt; 0.</param>
    /// <param name="hasOnOffSwitch">Whether the item has the vanilla on/off <c>state</c> (fireplaces do; smelters and ovens don't).</param>
    /// <param name="secondsPerFuel">
    /// For fireplaces, seconds one unit of fuel lasts (<c>m_secPerFuel</c>); 0 for items whose fuel use isn't
    /// purely time-based (smelters, ovens).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A value is out of range.</exception>
    public FuelItemInfo(float maxFuel, bool hasOnOffSwitch, float secondsPerFuel = 0f)
    {
        if (!(maxFuel > 0f) || float.IsInfinity(maxFuel))
        {
            throw new ArgumentOutOfRangeException(nameof(maxFuel), maxFuel, "Max fuel must be a finite number > 0.");
        }

        if (!(secondsPerFuel >= 0f) || float.IsInfinity(secondsPerFuel))
        {
            throw new ArgumentOutOfRangeException(nameof(secondsPerFuel), secondsPerFuel, "Seconds per fuel must be a finite number ≥ 0.");
        }

        MaxFuel = maxFuel;
        HasOnOffSwitch = hasOnOffSwitch;
        SecondsPerFuel = secondsPerFuel;
    }

    /// <summary>Fuel capacity.</summary>
    public float MaxFuel { get; }

    /// <summary>Whether the item can be switched off, and so can follow a schedule.</summary>
    public bool HasOnOffSwitch { get; }

    /// <summary>Seconds one unit of fuel lasts, or 0 when fuel use isn't time-based.</summary>
    public float SecondsPerFuel { get; }

    /// <summary>Whether the server can burn this item's fuel itself while nobody is nearby.</summary>
    public bool BurnsOverTime => HasOnOffSwitch && SecondsPerFuel > 0f;
}

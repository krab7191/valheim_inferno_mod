namespace Inferno.Core.Fuel;

/// <summary>
/// What the server currently sees in one item's network data (ZDO).
/// </summary>
public readonly struct FuelState
{
    /// <summary>Creates a snapshot.</summary>
    /// <param name="fuel">Current fuel (ZDO key <c>fuel</c>).</param>
    /// <param name="isSwitchedOn">Vanilla on/off switch (ZDO key <c>state</c> == 1). Always true for items without one.</param>
    /// <param name="switchedOffByInferno">Whether Inferno itself switched this item off (Inferno's own ZDO marker).</param>
    /// <param name="isUnattended">True when no player owns the item, i.e. nobody is nearby and nothing is burning it.</param>
    /// <param name="secondsSinceBurnClock">
    /// Seconds since the item's last-burn timestamp (ZDO key <c>lastTime</c>); 0 when unknown.
    /// </param>
    public FuelState(float fuel, bool isSwitchedOn, bool switchedOffByInferno, bool isUnattended, double secondsSinceBurnClock = 0)
    {
        Fuel = fuel;
        IsSwitchedOn = isSwitchedOn;
        SwitchedOffByInferno = switchedOffByInferno;
        IsUnattended = isUnattended;
        SecondsSinceBurnClock = secondsSinceBurnClock;
    }

    /// <summary>Current fuel.</summary>
    public float Fuel { get; }

    /// <summary>Vanilla on/off switch.</summary>
    public bool IsSwitchedOn { get; }

    /// <summary>Whether Inferno switched this item off.</summary>
    public bool SwitchedOffByInferno { get; }

    /// <summary>True when no player owns the item.</summary>
    public bool IsUnattended { get; }

    /// <summary>Seconds since the item's last-burn timestamp; 0 when unknown.</summary>
    public double SecondsSinceBurnClock { get; }
}

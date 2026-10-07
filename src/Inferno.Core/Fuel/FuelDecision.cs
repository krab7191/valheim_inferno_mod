namespace Inferno.Core.Fuel;

/// <summary>
/// What the server should write for one item after a scan. Null members mean "leave unchanged".
/// </summary>
public readonly struct FuelDecision
{
    /// <summary>Creates a decision.</summary>
    public FuelDecision(
        float? setFuel,
        bool? setSwitchedOn,
        bool? setSwitchedOffByInferno,
        bool refreshBurnClock,
        float baseline,
        bool mustWin = false)
    {
        SetFuel = setFuel;
        SetSwitchedOn = setSwitchedOn;
        SetSwitchedOffByInferno = setSwitchedOffByInferno;
        RefreshBurnClock = refreshBurnClock;
        Baseline = baseline;
        MustWin = mustWin;
    }

    /// <summary>New fuel value to write, or null.</summary>
    public float? SetFuel { get; }

    /// <summary>New on/off switch value to write, or null.</summary>
    public bool? SetSwitchedOn { get; }

    /// <summary>New value for Inferno's "switched off by Inferno" marker, or null.</summary>
    public bool? SetSwitchedOffByInferno { get; }

    /// <summary>Reset the item's last-burn timestamp to now (the server has accounted for the time until now).</summary>
    public bool RefreshBurnClock { get; }

    /// <summary>Fuel the item has after this decision; pass it back in on the next scan.</summary>
    public float Baseline { get; }

    /// <summary>
    /// The write should take precedence over a simultaneous update from the player who owns the item. True for
    /// always-on refills and schedule switches on owned items, where overwriting the player's update is harmless.
    /// False for burn-rate corrections, so a player's freshly added fuel is never overwritten.
    /// </summary>
    public bool MustWin { get; }

    /// <summary>Whether anything needs to be written.</summary>
    public bool HasChanges => SetFuel.HasValue || SetSwitchedOn.HasValue || SetSwitchedOffByInferno.HasValue || RefreshBurnClock;
}

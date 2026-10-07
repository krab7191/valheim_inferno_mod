namespace Inferno.Core.Ownership;

/// <summary>What to do with the ownership of one fire.</summary>
public enum OwnershipAction
{
    /// <summary>Leave as it is.</summary>
    None,

    /// <summary>Make the server the owner.</summary>
    Claim,

    /// <summary>Give it back to the game (owner 0); a nearby player becomes owner as in vanilla.</summary>
    Release,
}

/// <summary>
/// Server ownership mode: the server keeps ownership of fires, so no player's game runs the fire's owner logic
/// (burning fuel, switching off in rain). The server then does the burning itself, exactly.
/// </summary>
public static class OwnershipPolicy
{
    /// <summary>Decides what to do with one fire's ownership.</summary>
    /// <param name="modeEnabled">The <c>ServerOwnership</c> setting.</param>
    /// <param name="eligible">Whether this kind of fire may be server-owned (see <c>RTM.md</c> §4.13).</param>
    /// <param name="ownedByServer">Whether the server owns it now.</param>
    /// <param name="onLoan">Whether it is currently lent to a player (for deconstructing, damage, repair, …).</param>
    public static OwnershipAction Decide(bool modeEnabled, bool eligible, bool ownedByServer, bool onLoan)
    {
        if (!modeEnabled || !eligible)
        {
            return ownedByServer ? OwnershipAction.Release : OwnershipAction.None;
        }

        return onLoan || ownedByServer ? OwnershipAction.None : OwnershipAction.Claim;
    }
}

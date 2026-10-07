using System;

namespace Inferno.Core.Ownership;

/// <summary>
/// Server-side copies of the vanilla fireplace RPC handlers (decompiled, Valheim 1.0.17), used while the server
/// owns a fire. Without them a player's "add fuel" would be dropped, because the server has no instance of the fire
/// to run the real handler, and the player would lose the item.
/// </summary>
public static class FireplaceRpc
{
    /// <summary>Vanilla on/off value for "on" (ZDO key <c>state</c>).</summary>
    public const int StateOn = 1;

    /// <summary>Vanilla on/off value for "off".</summary>
    public const int StateOff = 2;

    /// <summary>
    /// <c>Fireplace.RPC_AddFuel</c>: adds one unit unless the fire already shows as full (fuel rounded up).
    /// </summary>
    public static float AddOne(float fuel, float maxFuel)
    {
        fuel = Sanitize(fuel);
        if (Math.Ceiling(fuel) >= maxFuel)
        {
            return fuel;
        }

        return Clamp(Clamp(fuel, maxFuel) + 1f, maxFuel);
    }

    /// <summary><c>Fireplace.RPC_AddFuelAmount</c>: adds (or removes) an amount, clamped to the tank.</summary>
    public static float AddAmount(float fuel, float amount, float maxFuel) =>
        Clamp(Sanitize(fuel) + Sanitize(amount, allowNegative: true), maxFuel);

    /// <summary>
    /// <c>Fireplace.RPC_SetFuelAmount</c>: sets the fuel. Vanilla trusts the caller; the server clamps it to the tank.
    /// </summary>
    public static float SetAmount(float fuel, float maxFuel) => Clamp(Sanitize(fuel), maxFuel);

    /// <summary><c>Fireplace.RPC_ToggleOn</c>: on becomes off, anything else becomes on.</summary>
    public static int Toggle(int state) => state == StateOn ? StateOff : StateOn;

    private static float Clamp(float value, float max) => Math.Max(0f, Math.Min(max, value));

    private static float Sanitize(float value, bool allowNegative = false) =>
        float.IsNaN(value) || float.IsInfinity(value) || (!allowNegative && value < 0f) ? 0f : value;
}

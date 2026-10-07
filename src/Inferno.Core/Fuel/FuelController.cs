using System;
using Inferno.Core.Settings;
using Inferno.Core.Time;

namespace Inferno.Core.Fuel;

/// <summary>
/// Decides, from the server side, what to write to one item so that it follows its settings.
/// </summary>
/// <remarks>
/// The server does not simulate fires: the player who owns an item burns its fuel (see RTM.md §4.2). The server
/// therefore watches the fuel value between scans and corrects it:
/// <list type="bullet">
/// <item>Always on: fuel is kept (nearly) full. Overrides the schedule.</item>
/// <item>Burn rate: any fuel lost since the last scan is scaled by the burn multiplier.</item>
/// <item>Schedule: the vanilla on/off switch is turned off outside the window (fuel is kept) and back on inside it.
/// Inferno only switches back on items it switched off itself, so it never overrides a player's own choice.</item>
/// <item>Ignore rain: vanilla rain/wind switches off uncovered switchable lights (owner-side, the server can't
/// prevent it). With this on, any switched-off light that should be on is switched back on. The server cannot tell
/// rain from a player's hand, so this also relights lights players switched off.</item>
/// <item>Nobody nearby: vanilla burns all the missed time in one go when a player arrives, ignoring schedules. For
/// fireplaces, the server instead burns the fuel itself as time passes (following schedule and burn rate) and
/// moves the item's burn clock forward, so the arriving player has nothing left to burn.</item>
/// </list>
/// </remarks>
public static class FuelController
{
    /// <summary>
    /// Minimum seconds between server-side burn steps for an item nobody is near. Keeps world writes rare; at
    /// schedule boundaries the burn is off by at most this much time.
    /// </summary>
    public const double UnattendedStepSeconds = 30.0;

    // Smaller differences are float noise and not worth a network write.
    private const float Epsilon = 0.0001f;

    // Always-on items are refilled once they are half a unit below full. The game rounds fuel up for display and
    // for "can't add more", so players still see a full fire and can't waste fuel on it, while the server writes
    // far less often than "refill on every tiny drop" would.
    private const float RefillMargin = 0.5f;

    /// <summary>Decides what to write for one item.</summary>
    /// <param name="settings">The item's settings.</param>
    /// <param name="item">Facts about the item's prefab.</param>
    /// <param name="state">What the server currently sees.</param>
    /// <param name="baseline">The <see cref="FuelDecision.Baseline"/> from the previous scan, or null on the first scan.</param>
    /// <param name="now">Current in-game time.</param>
    /// <param name="ignoreRain">The global <c>IgnoreRain</c> setting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> or <paramref name="item"/> is null.</exception>
    public static FuelDecision Decide(ItemSettings settings, FuelItemInfo item, FuelState state, float? baseline, TimeOfDay now, bool ignoreRain)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        // Corrupt or missing data reads as empty.
        var fuel = state.Fuel >= 0f ? state.Fuel : 0f;

        DecideSwitch(settings, item, state, now, ignoreRain, out var setSwitchedOn, out var setMarker);

        var simulate = state.IsUnattended && item.BurnsOverTime && state.SecondsSinceBurnClock >= UnattendedStepSeconds;
        float? setFuel;
        var refillForAlwaysOn = false;
        if (settings.AlwaysOn)
        {
            refillForAlwaysOn = fuel <= item.MaxFuel - Math.Min(RefillMargin, item.MaxFuel / 2f);
            setFuel = refillForAlwaysOn ? item.MaxFuel : null;
        }
        else
        {
            setFuel = ScaleFuelLoss(settings.BurnMultiplier, item.MaxFuel, fuel, baseline);
            if (simulate && state.IsSwitchedOn)
            {
                setFuel = BurnUnattended(setFuel ?? fuel, state.SecondsSinceBurnClock, item.SecondsPerFuel, settings.BurnMultiplier) ?? setFuel;
            }
        }

        // Overwriting a concurrent player update is harmless for refills and switches, never for corrections.
        var mustWin = !state.IsUnattended && (refillForAlwaysOn || setSwitchedOn.HasValue);
        return new FuelDecision(setFuel, setSwitchedOn, setMarker, simulate, setFuel ?? fuel, mustWin);
    }

    /// <summary>
    /// Undoes Inferno's schedule switch-offs. Called when the server shuts down so that, if Inferno is
    /// uninstalled before the next start, no fire is left switched off. Fuel is left as it is.
    /// </summary>
    public static FuelDecision RestoreForShutdown(FuelState state)
    {
        if (!state.SwitchedOffByInferno)
        {
            return new FuelDecision(null, null, null, false, state.Fuel);
        }

        return new FuelDecision(null, state.IsSwitchedOn ? null : true, false, false, state.Fuel);
    }

    private static void DecideSwitch(
        ItemSettings settings,
        FuelItemInfo item,
        FuelState state,
        TimeOfDay now,
        bool ignoreRain,
        out bool? setSwitchedOn,
        out bool? setMarker)
    {
        setSwitchedOn = null;
        setMarker = null;
        if (!item.HasOnOffSwitch)
        {
            return;
        }

        var shouldBeOn = settings.AlwaysOn || settings.Schedule.IsOnAt(now);
        if (!shouldBeOn)
        {
            if (state.IsSwitchedOn)
            {
                setSwitchedOn = false;
                setMarker = true;
            }

            return;
        }

        if (!state.IsSwitchedOn && (state.SwitchedOffByInferno || ignoreRain))
        {
            setSwitchedOn = true;
        }

        if (state.SwitchedOffByInferno)
        {
            // Also drops a stale marker when a player switched the item back on themselves.
            setMarker = false;
        }
    }

    private static float? ScaleFuelLoss(double multiplier, float maxFuel, float fuel, float? baseline)
    {
        if (baseline is not { } previous || fuel >= previous)
        {
            // First sighting, unchanged, or a player added fuel: nothing to correct.
            return null;
        }

        var burned = previous - fuel;
        var corrected = (float)Math.Max(0.0, Math.Min(maxFuel, previous - (burned * multiplier)));
        return Math.Abs(corrected - fuel) > Epsilon ? corrected : null;
    }

    private static float? BurnUnattended(float fuel, double seconds, float secondsPerFuel, double multiplier)
    {
        var burned = seconds / secondsPerFuel * multiplier;
        var remaining = (float)Math.Max(0.0, fuel - burned);
        return Math.Abs(remaining - fuel) > Epsilon ? remaining : null;
    }
}

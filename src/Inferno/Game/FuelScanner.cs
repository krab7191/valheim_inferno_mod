using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using Inferno.Core.Catalog;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;
using Inferno.Core.Time;

namespace Inferno.Game;

/// <summary>
/// Finds every fuel-burning object and sign in the world and applies the settings to them from the server.
/// All work happens on the main thread and is spread over frames.
/// </summary>
internal sealed class FuelScanner(PrefabDiscovery.Result discovery, ISettingsStore store, CommandService commands, ServerOwnership ownership, ManualLogSource log)
{
    private const float ApplyIntervalSeconds = 5f;
    private const float SweepIntervalSeconds = 30f;
    private const int SweepChunkSize = 20000;
    private const long FallbackDayLengthSeconds = 1800;
    private const uint RevisionLead = 8;

    /// <summary>Inferno's own ZDO key: 1 = switched off by Inferno's schedule. Vanilla ignores unknown keys.</summary>
    private static readonly int SwitchedOffByInfernoKey = "Inferno_ScheduledOff".GetStableHashCode();

    private static readonly AccessTools.FieldRef<ZDOMan, Dictionary<ZDOID, ZDO>> ObjectsById =
        AccessTools.FieldRefAccess<ZDOMan, Dictionary<ZDOID, ZDO>>("m_objectsByID");

    private readonly List<ZDO> _sweepBuffer = [];
    private readonly List<ZDOID> _sweepFuel = [];
    private readonly List<ZDOID> _sweepSigns = [];
    private readonly Dictionary<ZDOID, float> _baselines = [];
    private readonly Dictionary<string, ItemSettings> _settingsCache = [];
    private List<ZDOID> _fuel = [];
    private List<ZDOID> _signs = [];
    private int _sweepIndex = -1;
    private float _nextSweep;
    private float _nextApply;
    private bool _warnedDayLength;

    /// <summary>What the scanner currently tracks, for <c>!fires status</c>.</summary>
    public string StatusText
    {
        get
        {
            var owned = 0;
            foreach (var id in _fuel)
            {
                var zdo = ZDOMan.instance?.GetZDO(id);
                if (zdo is not null && ServerOwnership.IsServerSimulated(zdo))
                {
                    owned++;
                }
            }

            return $"Tracking {_fuel.Count} fuel object(s) ({owned} handled by the server) and {_signs.Count} sign(s).";
        }
    }

    /// <summary>The in-game clock as Inferno sees it, for <c>!fires status</c>.</summary>
    public string CurrentTimeText => CurrentTime().ToString();

    /// <summary>Call every frame.</summary>
    public void Tick(float now)
    {
        if (ZDOMan.instance is null || ZNet.instance is null)
        {
            return;
        }

        if (_sweepIndex < 0 && now >= _nextSweep)
        {
            BeginSweep();
        }

        if (_sweepIndex >= 0)
        {
            ContinueSweep(now);
        }

        ownership.Tick(now);
        if (now >= _nextApply)
        {
            _nextApply = now + ApplyIntervalSeconds;
            ApplySettings(now);
            HandleSigns();
        }
    }

    /// <summary>Gives all server-owned fires back to the game (shutdown, or Inferno stopping).</summary>
    public void ReleaseOwnership()
    {
        if (ZDOMan.instance is null)
        {
            return;
        }

        var released = ownership.ReleaseAll(ObjectsById(ZDOMan.instance).Values);
        if (released > 0)
        {
            log.LogInfo($"Gave {released} server-owned fire(s) back to the game.");
        }
    }

    /// <summary>
    /// Switches back on every fire Inferno's schedule switched off. Called right before the world is saved on
    /// shutdown, so that uninstalling Inferno never leaves fires dark. Fuel is left as it is.
    /// </summary>
    public void RestoreForShutdown()
    {
        if (ZDOMan.instance is null)
        {
            return;
        }

        var restored = 0;
        foreach (var zdo in ObjectsById(ZDOMan.instance).Values)
        {
            if (zdo is null || !discovery.ByHash.TryGetValue(zdo.GetPrefab(), out var item) || !item.CanSchedule)
            {
                continue;
            }

            var decision = FuelController.RestoreForShutdown(ReadState(zdo, item));
            if (decision.HasChanges)
            {
                Apply(zdo, decision);
                restored++;
            }
        }

        log.LogInfo($"Shutdown: switched {restored} scheduled-off light(s) back on before saving.");
    }

    private void BeginSweep()
    {
        _sweepBuffer.Clear();
        _sweepBuffer.AddRange(ObjectsById(ZDOMan.instance).Values);
        _sweepFuel.Clear();
        _sweepSigns.Clear();
        _sweepIndex = 0;
    }

    private void ContinueSweep(float now)
    {
        var end = Math.Min(_sweepIndex + SweepChunkSize, _sweepBuffer.Count);
        for (; _sweepIndex < end; _sweepIndex++)
        {
            var zdo = _sweepBuffer[_sweepIndex];
            if (zdo is null || !zdo.IsValid())
            {
                continue;
            }

            var prefab = zdo.GetPrefab();
            if (discovery.ByHash.ContainsKey(prefab))
            {
                _sweepFuel.Add(zdo.m_uid);
            }
            else if (discovery.SignHashes.Contains(prefab))
            {
                _sweepSigns.Add(zdo.m_uid);
            }
        }

        if (_sweepIndex < _sweepBuffer.Count)
        {
            return;
        }

        // Swap in the new lists; drop baselines of objects that no longer exist.
        (_fuel, _signs) = (new List<ZDOID>(_sweepFuel), new List<ZDOID>(_sweepSigns));
        var alive = new HashSet<ZDOID>(_fuel);
        var gone = new List<ZDOID>();
        foreach (var id in _baselines.Keys)
        {
            if (!alive.Contains(id))
            {
                gone.Add(id);
            }
        }

        foreach (var id in gone)
        {
            _baselines.Remove(id);
        }

        _sweepBuffer.Clear();
        _sweepIndex = -1;
        _nextSweep = now + SweepIntervalSeconds;
        log.LogDebug($"Sweep done: tracking {_fuel.Count} fuel object(s) and {_signs.Count} sign(s).");
    }

    private void ApplySettings(float realNow)
    {
        var now = CurrentTime();
        var ignoreRain = store.General.IgnoreRain;
        var writes = 0;
        _settingsCache.Clear();

        foreach (var id in _fuel)
        {
            var zdo = ZDOMan.instance.GetZDO(id);
            if (zdo is null || !discovery.ByHash.TryGetValue(zdo.GetPrefab(), out var item))
            {
                continue;
            }

            ownership.Manage(zdo, realNow);
            float? baseline = _baselines.TryGetValue(id, out var b) ? b : null;
            if (!_settingsCache.TryGetValue(item.PrefabName, out var settings))
            {
                settings = store.GetItem(item.PrefabName);
                _settingsCache[item.PrefabName] = settings;
            }

            var decision = FuelController.Decide(settings, item.Fuel, ReadState(zdo, item), baseline, now, ignoreRain);
            _baselines[id] = decision.Baseline;
            if (decision.HasChanges)
            {
                Apply(zdo, decision);
                writes++;
            }
        }

        if (writes > 0)
        {
            log.LogDebug($"Applied settings to {writes} of {_fuel.Count} object(s) at {now}.");
        }
    }

    private void HandleSigns()
    {
        foreach (var id in _signs)
        {
            var zdo = ZDOMan.instance.GetZDO(id);
            var text = zdo?.GetString(ZDOVars.s_text);
            if (zdo is null || string.IsNullOrEmpty(text) || !commands.HandleSign(zdo, text!))
            {
                continue;
            }

            // Clear the command so it runs once and the sign is reusable.
            zdo.Set(ZDOVars.s_text, string.Empty);
        }
    }

    private static FuelState ReadState(ZDO zdo, CatalogItem item)
    {
        var lastBurn = zdo.GetLong(ZDOVars.s_lastTime, 0L);
        var secondsSinceBurn = lastBurn > 0
            ? Math.Max(0.0, (ZNet.instance.GetTime().Ticks - lastBurn) / (double)TimeSpan.TicksPerSecond)
            : 0.0;
        return new FuelState(
            zdo.GetFloat(ZDOVars.s_fuel),
            !item.CanSchedule || zdo.GetInt(ZDOVars.s_state, 1) == 1,
            zdo.GetInt(SwitchedOffByInfernoKey) == 1,
            // Nobody's game is burning it: no owner, or the server owns it and has no instance (server ownership mode).
            !zdo.HasOwner() || ServerOwnership.IsServerSimulated(zdo),
            secondsSinceBurn);
    }

    private static void Apply(ZDO zdo, FuelDecision decision)
    {
        if (decision.SetFuel is { } fuel)
        {
            zdo.Set(ZDOVars.s_fuel, fuel);
        }

        if (decision.SetSwitchedOn is { } on)
        {
            zdo.Set(ZDOVars.s_state, on ? 1 : 2);
        }

        if (decision.SetSwitchedOffByInferno is { } marked)
        {
            zdo.Set(SwitchedOffByInfernoKey, marked ? 1 : 0);
        }

        if (decision.RefreshBurnClock)
        {
            zdo.Set(ZDOVars.s_lastTime, ZNet.instance.GetTime().Ticks);
        }

        if (decision.MustWin)
        {
            // The owning client only accepts data with a higher revision than its own copy. A fireplace owner
            // writes every 2 s, so a plain server write can lose that race. Jumping ahead a few revisions makes the
            // client accept ours; its next write then continues from our revision, so nothing gets stuck.
            zdo.DataRevision += RevisionLead;
        }
    }

    private TimeOfDay CurrentTime()
    {
        var dayLength = EnvMan.instance != null ? EnvMan.instance.m_dayLengthSec : 0L;
        if (dayLength <= 0)
        {
            if (!_warnedDayLength)
            {
                log.LogWarning($"Day length not available on this server; assuming {FallbackDayLengthSeconds} s for schedules.");
                _warnedDayLength = true;
            }

            dayLength = FallbackDayLengthSeconds;
        }

        return GameClock.TimeAt(Math.Max(0.0, ZNet.instance.GetTimeSeconds()), dayLength);
    }
}

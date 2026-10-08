using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using HarmonyLib;
using Inferno.Core.Areas;
using Inferno.Core.Catalog;
using Inferno.Core.Diagnostics;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;
using Inferno.Core.Time;
using UnityEngine;

namespace Inferno.Game;

/// <summary>
/// Finds every fuel-burning object and sign in the world and applies the settings to them from the server.
/// All work happens on the main thread and is spread over frames.
/// </summary>
internal sealed class FuelScanner(PrefabDiscovery.Result discovery, ISettingsStore store, CommandService commands, ServerOwnership ownership, ManualLogSource log)
{
    private const float ApplyIntervalSeconds = 5f;
    private const float SoonSeconds = 0.5f;
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

    // Objects seen arriving from players (new fires, edited signs) since the current sweep started.
    private readonly HashSet<ZDOID> _fuelSet = [];
    private readonly HashSet<ZDOID> _signSet = [];
    private readonly HashSet<ZDOID> _wardSet = [];
    private readonly List<ZDOID> _sweepWards = [];
    private readonly List<ZDOID> _arrivedSinceSweep = [];

    // Performance: logged every StatsIntervalSeconds so big worlds can be judged from the server log.
    private const float StatsIntervalSeconds = 600f;
    private readonly RunningStats _passStats = new();
    private readonly RunningStats _sweepStats = new();
    private readonly System.Diagnostics.Stopwatch _sweepWatch = new();
    private int _writesSinceStats;
    private float _nextStats = StatsIntervalSeconds;
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
            var unattended = 0;
            var own = 0;
            foreach (var id in _fuel)
            {
                var zdo = ZDOMan.instance?.GetZDO(id);
                if (zdo is null)
                {
                    continue;
                }

                if (ObjectSettings.Read(zdo) is not null)
                {
                    own++;
                }

                if (ServerOwnership.IsServerSimulated(zdo))
                {
                    owned++;
                }
                else if (!zdo.HasOwner())
                {
                    unattended++;
                }
            }

            return $"Tracking {_fuel.Count} fuel object(s): {unattended} with nobody nearby, {owned} handled by the server, {own} with own settings; {_signs.Count} sign(s), {_wardSet.Count} ward(s).";
        }
    }

    /// <summary>The in-game clock as Inferno sees it, for <c>!fires status</c>.</summary>
    public string CurrentTimeText => CurrentTime().ToString();

    /// <summary>
    /// Called when the server receives an object's data from a player. New fires are tracked right away and
    /// edited signs are read right away, instead of waiting for the next sweep (up to 30 s) or pass (5 s).
    /// </summary>
    public void Notice(ZDO zdo)
    {
        var prefab = zdo.GetPrefab();
        if (discovery.ByHash.ContainsKey(prefab))
        {
            if (_fuelSet.Add(zdo.m_uid))
            {
                _fuel.Add(zdo.m_uid);
                _arrivedSinceSweep.Add(zdo.m_uid);
                ApplySoon();
            }
        }
        else if (discovery.SignHashes.Contains(prefab))
        {
            if (_signSet.Add(zdo.m_uid))
            {
                _signs.Add(zdo.m_uid);
                _arrivedSinceSweep.Add(zdo.m_uid);
            }

            ApplySoon();
        }
        else if (discovery.WardRadii.ContainsKey(prefab))
        {
            _wardSet.Add(zdo.m_uid);
        }
    }

    /// <summary>All wards in the world, as the server's data describes them.</summary>
    public List<Ward> Wards()
    {
        var wards = new List<Ward>(_wardSet.Count);
        foreach (var id in _wardSet)
        {
            var zdo = ZDOMan.instance?.GetZDO(id);
            if (zdo is null || !discovery.WardRadii.TryGetValue(zdo.GetPrefab(), out var radius))
            {
                continue;
            }

            // Same data PrivateArea reads: enabled flag, builder, and the permitted list (count + pu_id0..n).
            var permitted = new List<long>();
            var count = zdo.GetInt(ZDOVars.s_permitted);
            for (var i = 0; i < count; i++)
            {
                permitted.Add(zdo.GetLong("pu_id" + i.ToString(CultureInfo.InvariantCulture)));
            }

            var position = zdo.GetPosition();
            wards.Add(new Ward(position.x, position.z, radius, zdo.GetBool(ZDOVars.s_enabled), zdo.GetLong(ZDOVars.s_creator), permitted));
        }

        return wards;
    }

    /// <summary>The fires around a spot for a "nearby" command, with the player's ward access to each.</summary>
    public NearbySelection Nearby(Vector3 spot, long playerId)
    {
        var wards = Wards();
        var area = NearbyArea.Around(wards, spot.x, spot.z);
        var objects = new List<NearbyObject>();
        foreach (var id in _fuel)
        {
            var zdo = ZDOMan.instance?.GetZDO(id);
            if (zdo is null || !discovery.ByHash.TryGetValue(zdo.GetPrefab(), out var item))
            {
                continue;
            }

            var position = zdo.GetPosition();
            if (area.Contains(position.x, position.z))
            {
                objects.Add(new NearbyObject(ObjectSettings.KeyOf(id), item.PrefabName, NearbyArea.CanAccess(wards, position.x, position.z, playerId)));
            }
        }

        return new NearbySelection(objects, area.Description);
    }

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
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ApplySettings(now);
            HandleSigns();
            _passStats.Add(watch.Elapsed.TotalMilliseconds);
        }

        if (now >= _nextStats)
        {
            _nextStats = now + StatsIntervalSeconds;
            log.LogInfo($"Performance (last {StatsIntervalSeconds / 60:0} min): {StatusText} Passes: {_passStats}. Sweeps: {_sweepStats}. Writes: {_writesSinceStats}. Errors since start: {Diagnostics.ErrorCount}.");
            _passStats.Reset();
            _sweepStats.Reset();
            _writesSinceStats = 0;
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

            try
            {
                var decision = FuelController.RestoreForShutdown(ReadState(zdo, item));
                if (decision.HasChanges)
                {
                    Apply(zdo, decision);
                    restored++;
                }
            }
            catch (Exception e)
            {
                Diagnostics.Error($"switching {item.PrefabName} back on at shutdown", e);
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
        _sweepWards.Clear();
        _arrivedSinceSweep.Clear();
        _sweepIndex = 0;
    }

    private void ContinueSweep(float now)
    {
        _sweepWatch.Start();
        var end = Math.Min(_sweepIndex + SweepChunkSize, _sweepBuffer.Count);
        for (; _sweepIndex < end; _sweepIndex++)
        {
            try
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
                else if (discovery.WardRadii.ContainsKey(prefab))
                {
                    _sweepWards.Add(zdo.m_uid);
                }
            }
            catch (Exception e)
            {
                Diagnostics.Error("looking for fires and signs in the world", e);
            }
        }

        _sweepWatch.Stop();
        if (_sweepIndex < _sweepBuffer.Count)
        {
            return;
        }

        // One sweep spans several frames; record the total time it spent.
        _sweepStats.Add(_sweepWatch.Elapsed.TotalMilliseconds);
        _sweepWatch.Reset();

        // Swap in the new lists (plus anything that arrived during the sweep); drop baselines of objects that no
        // longer exist.
        _fuelSet.Clear();
        _signSet.Clear();
        _fuelSet.UnionWith(_sweepFuel);
        _signSet.UnionWith(_sweepSigns);

        // Wards placed during the sweep stay known (Notice adds them); removed ones drop out here.
        _wardSet.RemoveWhere(id => ZDOMan.instance?.GetZDO(id) is null);
        _wardSet.UnionWith(_sweepWards);
        foreach (var id in _arrivedSinceSweep)
        {
            var zdo = ZDOMan.instance?.GetZDO(id);
            if (zdo is null)
            {
                continue;
            }

            if (discovery.ByHash.ContainsKey(zdo.GetPrefab()))
            {
                _fuelSet.Add(id);
            }
            else
            {
                _signSet.Add(id);
            }
        }

        (_fuel, _signs) = (new List<ZDOID>(_fuelSet), new List<ZDOID>(_signSet));
        _arrivedSinceSweep.Clear();
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

            try
            {
                ownership.Manage(zdo, realNow);
                float? baseline = _baselines.TryGetValue(id, out var b) ? b : null;
                // A fire's own settings ("nearby" commands) win over its item type's.
                var settings = ObjectSettings.Read(zdo);
                if (settings is null && !_settingsCache.TryGetValue(item.PrefabName, out settings))
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
                    _writesSinceStats++;
                }
            }
            catch (Exception e)
            {
                Diagnostics.Error($"applying settings to {item.PrefabName}", e);
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
            if (zdo is null || string.IsNullOrEmpty(text))
            {
                continue;
            }

            string answer;
            try
            {
                if (!commands.HandleSign(zdo, text!, out answer))
                {
                    continue;
                }
            }
            catch (Exception e)
            {
                // Replace the command anyway, so a failing command doesn't run again every few seconds.
                Diagnostics.Error("running a sign command", e);
                answer = "Inferno error; see server log.";
            }

            // Replace the command with the short answer: it runs once, the result stays readable, and writing the
            // next command over it reuses the sign.
            zdo.Set(ZDOVars.s_text, answer);
        }
    }

    // Run the next pass shortly (batched: many arrivals in one moment still mean one pass).
    private void ApplySoon() => _nextApply = Math.Min(_nextApply, UnityEngine.Time.realtimeSinceStartup + SoonSeconds);

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

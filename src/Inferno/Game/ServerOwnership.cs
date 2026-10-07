using System;
using System.Collections.Generic;
using BepInEx.Logging;
using HarmonyLib;
using Inferno.Core.Ownership;
using Inferno.Core.Settings;

namespace Inferno.Game;

/// <summary>
/// Experimental "server ownership" mode (RTM.md §4.13). The server keeps ownership of eligible fires so no player's
/// game runs their owner logic: no client burns fuel and rain can't switch them off. The server burns fuel itself.
/// </summary>
/// <remarks>
/// Player actions on a fire are routed to its owner. The server has no instance of the fire, so vanilla would drop
/// them. Fuel and on/off actions are therefore handled here; anything else (deconstructing, damage, repair, …) lends
/// the fire to that player for a short time and passes the action on, so the game handles it normally.
/// </remarks>
internal sealed class ServerOwnership(PrefabDiscovery.Result discovery, ISettingsStore store, ManualLogSource log)
{
    private const double LoanSeconds = 30.0;
    private const double ForwardDelaySeconds = 1.0;

    private static readonly int AddFuelHash = "RPC_AddFuel".GetStableHashCode();
    private static readonly int AddFuelAmountHash = "RPC_AddFuelAmount".GetStableHashCode();
    private static readonly int SetFuelAmountHash = "RPC_SetFuelAmount".GetStableHashCode();
    private static readonly int ToggleOnHash = "RPC_ToggleOn".GetStableHashCode();

    private static readonly Action<ZRoutedRpc, ZRoutedRpc.RoutedRPCData> RouteRpc =
        AccessTools.MethodDelegate<Action<ZRoutedRpc, ZRoutedRpc.RoutedRPCData>>(AccessTools.Method(typeof(ZRoutedRpc), "RouteRPC"));

    private readonly LoanBook<ZDOID> _loans = new();
    private readonly Dictionary<ZDOID, Heightmap.Biome> _biomes = [];
    private readonly List<(double At, ZRoutedRpc.RoutedRPCData Data)> _forwards = [];

    public static long ServerId => ZDOMan.GetSessionID();

    public bool Enabled => store.General.ServerOwnership;

    /// <summary>True when the server owns the object and simulates it itself (no local instance).</summary>
    public static bool IsServerSimulated(ZDO zdo) =>
        zdo.GetOwner() == ServerId && ZNetScene.instance.FindInstance(zdo) == null;

    /// <summary>Claims or releases ownership of one tracked fire according to the setting.</summary>
    public void Manage(ZDO zdo, double now)
    {
        var action = OwnershipPolicy.Decide(Enabled, IsEligible(zdo), zdo.GetOwner() == ServerId, _loans.IsOnLoan(zdo.m_uid, now));
        if (action == OwnershipAction.Claim)
        {
            zdo.SetOwner(ServerId);
        }
        else if (action == OwnershipAction.Release)
        {
            zdo.SetOwner(0L);
        }
    }

    /// <summary>Whether vanilla's automatic owner reassignment must leave this object alone.</summary>
    public bool IsProtected(ZDO zdo) =>
        Enabled
        && zdo.GetOwner() == ServerId
        && IsEligible(zdo)
        && !_loans.IsOnLoan(zdo.m_uid, UnityEngine.Time.realtimeSinceStartup);

    /// <summary>
    /// Whether this particular fire may be server-owned: its type has no other owner-run parts, and its extras
    /// (snow melting, fire spreading) don't apply where it stands.
    /// </summary>
    public bool IsEligible(ZDO zdo)
    {
        if (!discovery.OwnershipCandidates.TryGetValue(zdo.GetPrefab(), out var extras))
        {
            return false;
        }

        if (extras == PrefabDiscovery.FireExtras.None)
        {
            return true;
        }

        var biome = BiomeOf(zdo);
        if ((extras & PrefabDiscovery.FireExtras.MeltsSnow) != 0 && biome == Heightmap.Biome.DeepNorth)
        {
            return false;
        }

        return (extras & PrefabDiscovery.FireExtras.SpreadsFire) == 0
            || (biome != Heightmap.Biome.AshLands && !ZoneSystem.instance.GetGlobalKey(GlobalKeys.Fire));
    }

    // Pieces don't move, so each fire's biome is looked up once.
    private Heightmap.Biome BiomeOf(ZDO zdo)
    {
        if (!_biomes.TryGetValue(zdo.m_uid, out var biome))
        {
            biome = WorldGenerator.instance?.GetBiome(zdo.GetPosition()) ?? Heightmap.Biome.None;
            _biomes[zdo.m_uid] = biome;
        }

        return biome;
    }

    /// <summary>
    /// Handles an RPC sent to a fire the server owns. Returns true when handled here (vanilla must not run).
    /// </summary>
    public bool TryHandleRpc(ZRoutedRpc.RoutedRPCData data)
    {
        if (data.m_targetZDO.IsNone() || data.m_targetPeerID != ServerId)
        {
            return false;
        }

        var zdo = ZDOMan.instance.GetZDO(data.m_targetZDO);
        if (zdo is null || !IsServerSimulated(zdo) || !discovery.ByHash.TryGetValue(zdo.GetPrefab(), out var item) || !item.Fuel.BurnsOverTime)
        {
            return false;
        }

        var hash = data.m_methodHash;
        var pkg = new ZPackage(data.m_parameters.GetArray());
        var fuel = zdo.GetFloat(ZDOVars.s_fuel);
        var max = item.Fuel.MaxFuel;
        if (hash == AddFuelHash)
        {
            zdo.Set(ZDOVars.s_fuel, FireplaceRpc.AddOne(fuel, max));
        }
        else if (hash == AddFuelAmountHash)
        {
            zdo.Set(ZDOVars.s_fuel, FireplaceRpc.AddAmount(fuel, pkg.ReadSingle(), max));
        }
        else if (hash == SetFuelAmountHash)
        {
            zdo.Set(ZDOVars.s_fuel, FireplaceRpc.SetAmount(pkg.ReadSingle(), max));
        }
        else if (hash == ToggleOnHash)
        {
            zdo.Set(ZDOVars.s_state, FireplaceRpc.Toggle(zdo.GetInt(ZDOVars.s_state, FireplaceRpc.StateOn)));
        }
        else
        {
            Lend(zdo, data, item.PrefabName);
            return true;
        }

        log.LogDebug($"Handled {MethodName(hash)} on server-owned {item.PrefabName} from peer {data.m_senderPeerID}.");
        return true;
    }

    /// <summary>Sends delayed forwards and forgets expired loans. Call every frame.</summary>
    public void Tick(double now)
    {
        for (var i = _forwards.Count - 1; i >= 0; i--)
        {
            if (now < _forwards[i].At)
            {
                continue;
            }

            var data = _forwards[i].Data;
            _forwards.RemoveAt(i);
            try
            {
                RouteRpc(ZRoutedRpc.instance, data);
            }
            catch (Exception e)
            {
                Diagnostics.Error("passing a player's action on to them", e);
            }
        }

        _loans.Prune(now);
    }

    /// <summary>Gives every server-owned fire back to the game (on shutdown or when Inferno stops).</summary>
    public int ReleaseAll(IEnumerable<ZDO> zdos)
    {
        var released = 0;
        foreach (var zdo in zdos)
        {
            try
            {
                if (zdo is not null && zdo.GetOwner() == ServerId && discovery.OwnershipCandidates.ContainsKey(zdo.GetPrefab()))
                {
                    zdo.SetOwner(0L);
                    released++;
                }
            }
            catch (Exception e)
            {
                Diagnostics.Error("giving a server-owned fire back", e);
            }
        }

        _loans.Clear();
        _forwards.Clear();
        return released;
    }

    private void Lend(ZDO zdo, ZRoutedRpc.RoutedRPCData data, string prefabName)
    {
        var sender = data.m_senderPeerID;
        var now = UnityEngine.Time.realtimeSinceStartup;
        _loans.Lend(zdo.m_uid, now, LoanSeconds);
        if (sender == 0L || sender == ServerId)
        {
            zdo.SetOwner(0L);
            return;
        }

        // The player becomes owner; once that has reached their game, their own action is handed to them.
        zdo.SetOwner(sender);
        var forward = new ZRoutedRpc.RoutedRPCData
        {
            m_msgID = data.m_msgID,
            m_senderPeerID = sender,
            m_targetPeerID = sender,
            m_targetZDO = data.m_targetZDO,
            m_methodHash = data.m_methodHash,
            m_parameters = new ZPackage(data.m_parameters.GetArray()),
        };
        _forwards.Add((now + ForwardDelaySeconds, forward));
        log.LogDebug($"Lent server-owned {prefabName} to peer {sender} for an action ({data.m_methodHash}).");
    }

    private static string MethodName(int hash) =>
        hash == AddFuelHash ? "add fuel"
        : hash == AddFuelAmountHash ? "add fuel amount"
        : hash == SetFuelAmountHash ? "set fuel"
        : "switch on/off";
}

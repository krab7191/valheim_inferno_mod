using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Inferno.Core.Catalog;
using Inferno.Core.Fuel;
using Inferno.Core.Settings;
using UnityEngine;

namespace Inferno.Game;

/// <summary>
/// Finds every fuel-burning prefab (and every sign prefab) the game knows about, including modded ones.
/// </summary>
internal static class PrefabDiscovery
{
    /// <summary>Owner-run extras a fire type has that only matter in some places.</summary>
    [Flags]
    public enum FireExtras
    {
        None = 0,

        /// <summary>Melts snow around it (Deep North only).</summary>
        MeltsSnow = 1,

        /// <summary>Can set nearby pieces on fire (Ashlands, or anywhere with the world's fire modifier).</summary>
        SpreadsFire = 2,
    }

    public sealed class Result(ItemCatalog catalog, Dictionary<int, CatalogItem> byHash, HashSet<int> signHashes, Dictionary<int, FireExtras> ownershipCandidates)
    {
        /// <summary>
        /// Fire types the server may own in server ownership mode, with the extras that make a particular fire
        /// ineligible depending on where it stands.
        /// </summary>
        public Dictionary<int, FireExtras> OwnershipCandidates { get; } = ownershipCandidates;

        public ItemCatalog Catalog { get; } = catalog;

        /// <summary>Catalog items by prefab hash (what a ZDO stores).</summary>
        public Dictionary<int, CatalogItem> ByHash { get; } = byHash;

        /// <summary>Prefab hashes of all signs, for sign commands.</summary>
        public HashSet<int> SignHashes { get; } = signHashes;
    }

    public static Result Run(ZNetScene scene, ManualLogSource log, bool verbose = true)
    {
        var items = new List<CatalogItem>();
        var byHash = new Dictionary<int, CatalogItem>();
        var signs = new HashSet<int>();
        var rainSensitive = new List<string>();
        var ownershipCandidates = new Dictionary<int, FireExtras>();
        var ownershipExcluded = new List<string>();
        var ownershipConditional = new List<string>();

        foreach (var prefab in scene.m_prefabs)
        {
            if (prefab == null)
            {
                continue;
            }

            if (prefab.GetComponent<Sign>() != null)
            {
                signs.Add(prefab.name.GetStableHashCode());
            }

            if (IsRainSensitive(prefab))
            {
                rainSensitive.Add(prefab.name);
            }

            var item = TryCreateItem(prefab, log);
            if (item is null || byHash.ContainsKey(prefab.name.GetStableHashCode()))
            {
                continue;
            }

            items.Add(item);
            byHash.Add(prefab.name.GetStableHashCode(), item);
            if (item.Fuel.BurnsOverTime)
            {
                if (HasOtherOwnerParts(prefab))
                {
                    ownershipExcluded.Add(prefab.name);
                }
                else
                {
                    var extras = ExtrasOf(prefab.GetComponent<Fireplace>());
                    ownershipCandidates.Add(prefab.name.GetStableHashCode(), extras);
                    if (extras != FireExtras.None)
                    {
                        ownershipConditional.Add($"{prefab.name} ({extras})");
                    }
                }
            }
        }

        var catalog = new ItemCatalog(items);
        if (!verbose)
        {
            return new Result(catalog, byHash, signs, ownershipCandidates);
        }

        foreach (var item in catalog.Items)
        {
            log.LogInfo($"Found {(item.Kind == ItemKind.LightSource ? "light source" : "fuel station")}: {item.PrefabName} ({item.DisplayName}), max fuel {item.Fuel.MaxFuel}");
        }

        log.LogInfo($"Discovered {catalog.Items.Count} fuel-burning item types and {signs.Count} sign types.");
        log.LogInfo(rainSensitive.Count == 0
            ? "No lights switch themselves off in rain."
            : $"Lights that switch themselves off in rain or strong wind when uncovered (see IgnoreRain): {string.Join(", ", rainSensitive)}");
        log.LogInfo($"Server ownership mode can manage {ownershipCandidates.Count} fire type(s)."
            + (ownershipConditional.Count == 0 ? string.Empty : $" Except where their extras apply (MeltsSnow: Deep North; SpreadsFire: Ashlands or fire modifier): {string.Join(", ", ownershipConditional)}.")
            + (ownershipExcluded.Count == 0 ? string.Empty : $" Never (other owner-run parts): {string.Join(", ", ownershipExcluded)}."));
        return new Result(catalog, byHash, signs, ownershipCandidates);
    }

    private static CatalogItem? TryCreateItem(GameObject prefab, ManualLogSource log)
    {
        try
        {
            var fireplace = prefab.GetComponent<Fireplace>();
            if (fireplace != null)
            {
                // Infinite-fuel fires (e.g. Haldor's) and fires that never burn fuel need nothing from Inferno.
                return fireplace.m_infiniteFuel || fireplace.m_secPerFuel <= 0f || fireplace.m_maxFuel <= 0f
                    ? null
                    : Create(prefab, fireplace.m_name, ItemKind.LightSource, new FuelItemInfo(fireplace.m_maxFuel, hasOnOffSwitch: true, fireplace.m_secPerFuel));
            }

            var smelter = prefab.GetComponent<Smelter>();
            if (smelter != null)
            {
                // Pieces like the charcoal kiln take wood as ore and have no fuel. A smelter that has no ore slots and
                // produces nothing (the hot tub) just burns fuel while lit, like a fire, so it counts as a light
                // source (always on by default; no on/off switch, so no schedule). One that turns its fuel into an
                // item (the Frigid Kiln: ice → Liquid Frost) is a production station and stays vanilla by default.
                if (smelter.m_maxFuel <= 0 || smelter.m_fuelItem == null)
                {
                    return null;
                }

                var producesItems = smelter.m_conversion is not null && smelter.m_conversion.Exists(c => c?.m_to != null);
                var kind = smelter.m_maxOre <= 0 && !producesItems ? ItemKind.LightSource : ItemKind.FuelStation;
                return Create(prefab, smelter.m_name, kind, new FuelItemInfo(smelter.m_maxFuel, hasOnOffSwitch: false));
            }

            var station = prefab.GetComponent<CookingStation>();
            if (station != null)
            {
                return !station.m_useFuel || station.m_maxFuel <= 0
                    ? null
                    : Create(prefab, station.m_name, ItemKind.FuelStation, new FuelItemInfo(station.m_maxFuel, hasOnOffSwitch: false));
            }
        }
        catch (Exception e) when (e is ArgumentException or NullReferenceException)
        {
            // A broken modded prefab must not stop discovery of everything else.
            log.LogWarning($"Skipping prefab '{prefab.name}': {e.Message}");
        }

        return null;
    }

    // Fires whose owner runs other logic must stay player-owned, or that logic would stop.
    private static bool HasOtherOwnerParts(GameObject prefab) =>
        prefab.GetComponentInChildren<Smelter>() != null
        || prefab.GetComponentInChildren<CookingStation>() != null
        || prefab.GetComponentInChildren<Container>() != null
        || prefab.GetComponentInChildren<Fermenter>() != null
        || prefab.GetComponentInChildren<Beehive>() != null
        || prefab.GetComponentInChildren<Ship>() != null;

    // Fireplace.UpdateSnowMelt only melts in the Deep North; Fireplace.UpdateIgnite only spreads where
    // CinderSpawner.CanSpawnCinder allows it (Ashlands, or the world's fire modifier).
    private static FireExtras ExtrasOf(Fireplace fireplace) =>
        (fireplace.m_snowMelter != null ? FireExtras.MeltsSnow : FireExtras.None)
        | (fireplace.m_igniteInterval > 0f && fireplace.m_igniteCapsuleRadius > 0f && fireplace.m_firePrefab != null ? FireExtras.SpreadsFire : FireExtras.None);

    // Mirrors Fireplace.CheckEnv/UpdateState: only switchable fires with separate low/high flame objects react to rain.
    private static bool IsRainSensitive(GameObject prefab)
    {
        var fireplace = prefab.GetComponent<Fireplace>();
        return fireplace != null && fireplace.m_canTurnOff && fireplace.m_enabledObjectLow != null && fireplace.m_enabledObjectHigh != null;
    }

    // The build-menu name (Piece.m_name) is what players know; many fires share the component name "Fire".
    private static CatalogItem Create(GameObject prefab, string componentName, ItemKind kind, FuelItemInfo fuel)
    {
        var piece = prefab.GetComponent<Piece>();
        var token = piece != null && !string.IsNullOrWhiteSpace(piece.m_name) ? piece.m_name : componentName;
        return new(prefab.name, Localize(token, prefab.name), kind, fuel);
    }

    private static string Localize(string token, string fallback)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return fallback;
        }

        try
        {
            var text = Localization.instance?.Localize(token);
            return string.IsNullOrWhiteSpace(text) ? token : text!;
        }
        catch (Exception e) when (e is NullReferenceException or InvalidOperationException)
        {
            // Localization may not be fully set up on a dedicated server.
            return token;
        }
    }
}

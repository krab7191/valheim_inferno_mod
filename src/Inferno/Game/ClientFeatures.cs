using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Inferno.Game;

/// <summary>
/// Features that only work in a player's own game (optional client mod): removing smoke and ignoring rain for the
/// fires this player's game runs. Active only while this game hosts an Inferno world or is synced with an Inferno
/// server, so a client on a vanilla server stays vanilla.
/// </summary>
internal static class ClientFeatures
{
    private static readonly Dictionary<int, ZNetView?> ViewBySpawner = [];

    /// <summary>Set by the plugin; null until loaded.</summary>
    public static ConfigSettingsStore? Store { get; set; }

    private static bool Active => Store is not null && (Store.IsMirroring || InfernoRuntime.Current is not null);

    /// <summary>
    /// Smoke off: skip creating smoke puffs, but keep vanilla's "smoke is blocked" bookkeeping (a fire whose smoke
    /// can't escape still goes out, as in vanilla).
    /// </summary>
    [HarmonyPatch(typeof(SmokeSpawner), "Spawn")]
    private static class SmokePatch
    {
        private static readonly AccessTools.FieldRef<SmokeSpawner, float> LastSpawnTime =
            AccessTools.FieldRefAccess<SmokeSpawner, float>("m_lastSpawnTime");

        private static readonly Func<SmokeSpawner, bool> TestBlocked =
            AccessTools.MethodDelegate<Func<SmokeSpawner, bool>>(AccessTools.Method(typeof(SmokeSpawner), "TestBlocked"));

        private static bool Prefix(SmokeSpawner __instance, float time)
        {
            if (!Active || SmokeWanted(__instance))
            {
                return true;
            }

            if (!TestBlocked(__instance))
            {
                LastSpawnTime(__instance) = time;
            }

            return false;
        }

        // A fire's own settings (synced in its world data) win over its item type's.
        private static bool SmokeWanted(SmokeSpawner spawner)
        {
            var id = spawner.GetInstanceID();
            if (!ViewBySpawner.TryGetValue(id, out var view))
            {
                view = spawner.GetComponentInParent<ZNetView>();
                ViewBySpawner[id] = view;
            }

            if (view == null || !view.IsValid())
            {
                return true;
            }

            return ObjectSettings.Read(view.GetZDO())?.Smoke ?? Store!.GetItem(Utils.GetPrefabName(view.gameObject)).Smoke;
        }
    }

    /// <summary>
    /// Ignore rain: this player's game never considers its fires wet, so it never switches them off in rain.
    /// (The server alone can only relight them afterwards.)
    /// </summary>
    [HarmonyPatch(typeof(Fireplace), "CheckWet")]
    private static class RainPatch
    {
        private static readonly AccessTools.FieldRef<Fireplace, bool> Wet = AccessTools.FieldRefAccess<Fireplace, bool>("m_wet");

        private static void Postfix(Fireplace __instance)
        {
            if (Active && Store!.General.IgnoreRain)
            {
                Wet(__instance) = false;
            }
        }
    }
}

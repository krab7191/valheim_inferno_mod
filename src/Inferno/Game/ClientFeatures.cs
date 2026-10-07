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
    private static readonly Dictionary<int, string> PrefabBySpawner = [];

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
            if (!Active || Store!.GetItem(PrefabOf(__instance)).Smoke)
            {
                return true;
            }

            if (!TestBlocked(__instance))
            {
                LastSpawnTime(__instance) = time;
            }

            return false;
        }

        private static string PrefabOf(SmokeSpawner spawner)
        {
            var id = spawner.GetInstanceID();
            if (!PrefabBySpawner.TryGetValue(id, out var name))
            {
                var view = spawner.GetComponentInParent<ZNetView>();
                name = view != null ? Utils.GetPrefabName(view.gameObject) : string.Empty;
                PrefabBySpawner[id] = name;
            }

            return name;
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

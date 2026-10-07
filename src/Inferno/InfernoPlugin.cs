using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Inferno.Game;
using UnityEngine;

namespace Inferno;

/// <summary>
/// BepInEx entry point. Inferno only acts on a server; on a client it stays idle so that installing it
/// client-side can never change gameplay or cause desyncs.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[SuppressMessage("Design", "CA1001", Justification = "Unity owns the lifecycle; resources are released in OnDestroy.")]
public sealed class InfernoPlugin : BaseUnityPlugin
{
    /// <summary>The Valheim version this build was tested against (see README compatibility table).</summary>
    private const string TestedGameVersion = "1.0.17";

    private Harmony? _harmony;
    private ConfigSettingsStore? _store;
    private ClientSync? _client;
    private bool _clientFailed;
    private bool _failed;

    private void Awake()
    {
        _store = new ConfigSettingsStore(Config, Logger);
        _client = new ClientSync(_store, Logger);
        ClientFeatures.Store = _store;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        ApplyPatches(_harmony);

        var gameVersion = Version.CurrentVersion.ToString();
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded (Valheim {gameVersion}).");
        if (gameVersion != TestedGameVersion)
        {
            Logger.LogWarning($"This Inferno build was tested on Valheim {TestedGameVersion}; this server runs {gameVersion}. It will probably work, but please report problems.");
        }
    }

    // Start/stop by watching the game state instead of patching scene setup order, which differs between
    // dedicated servers and hosted games.
    private void Update()
    {
        TickClient();

        var serverWorldLoaded = ZNet.instance != null && ZNet.instance.IsServer()
            && ZNetScene.instance != null && ZDOMan.instance != null;

        if (!serverWorldLoaded)
        {
            if (InfernoRuntime.Current is not null)
            {
                InfernoRuntime.Stop();
                Logger.LogInfo("World unloaded; Inferno stopped.");
            }

            _failed = false;
            return;
        }

        if (_failed)
        {
            return;
        }

        try
        {
            if (InfernoRuntime.Current is null)
            {
                InfernoRuntime.Start(_store!, Logger);
            }

            InfernoRuntime.Current!.Tick(Time.realtimeSinceStartup);
        }
        catch (Exception e)
        {
            // Never break the server: disable Inferno for this session and leave the game vanilla.
            _failed = true;
            InfernoRuntime.Stop();
            Logger.LogError($"Inferno hit an unexpected error and is disabled until the world is reloaded. {e}");
        }
    }

    // Each hook is patched on its own: if a game update renames one hooked method, only that feature (chat,
    // console or shutdown restore) is disabled, with a clear message, instead of the whole mod failing to load.
    private void ApplyPatches(Harmony harmony)
    {
        var hooks = new List<Type>(typeof(GameHooks).GetNestedTypes(BindingFlags.NonPublic));
        hooks.AddRange(typeof(ClientFeatures).GetNestedTypes(BindingFlags.NonPublic));
        foreach (var patch in hooks)
        {
            try
            {
                harmony.CreateClassProcessor(patch).Patch();
            }
            catch (Exception e)
            {
                Logger.LogError($"Could not install hook {patch.Name}; that feature is disabled. Likely cause: a Valheim update. {e.Message}");
            }
        }
    }

    // Client side (optional client mod): menu sync with an Inferno server. Failures only disable the client
    // features until the game restarts; they never affect playing.
    private void TickClient()
    {
        if (_clientFailed)
        {
            return;
        }

        try
        {
            _client!.Tick(Time.realtimeSinceStartup);
        }
        catch (Exception e)
        {
            _clientFailed = true;
            Logger.LogError($"Inferno client features hit an unexpected error and are disabled until restart. {e}");
        }
    }

    private void OnDestroy()
    {
        InfernoRuntime.Stop();
        _store?.Dispose();
        _harmony?.UnpatchSelf();
    }
}

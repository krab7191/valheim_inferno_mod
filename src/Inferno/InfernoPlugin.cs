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

    // A failing frame is retried; only errors on every frame for this long switch Inferno off for the session.
    private const float GiveUpAfterSeconds = 30f;

    private Harmony? _harmony;
    private float _failingSince = -1f;
    private ConfigSettingsStore? _store;
    private ClientSync? _client;
    private bool _clientFailed;
    private bool _failed;

    private void Awake()
    {
        Diagnostics.Init(Logger);
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

        if (InfernoRuntime.Current is null)
        {
            try
            {
                InfernoRuntime.Start(_store!, Logger, _harmony);
            }
            catch (Exception e)
            {
                // Can't run without a working start: leave the game vanilla for this session.
                _failed = true;
                InfernoRuntime.Stop();
                Logger.LogError($"Inferno could not start and is disabled until the world is reloaded; the game runs as vanilla. Please report this with the log (Inferno {MyPluginInfo.PLUGIN_VERSION}, Valheim {Version.CurrentVersion}):\n{e}");
                return;
            }
        }

        var now = Time.realtimeSinceStartup;
        try
        {
            InfernoRuntime.Current!.Tick(now);
            _failingSince = -1f;
        }
        catch (Exception e)
        {
            Diagnostics.Error("running Inferno's regular update", e);
            if (_failingSince < 0f)
            {
                _failingSince = now;
            }
            else if (now - _failingSince > GiveUpAfterSeconds)
            {
                // Never break the server: after a sustained failure, disable Inferno and leave the game vanilla.
                _failed = true;
                InfernoRuntime.Stop();
                Logger.LogError($"Inferno kept failing for {GiveUpAfterSeconds:0} s and is disabled until the world is reloaded; the game runs as vanilla. See the first error above.");
            }
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
            Diagnostics.Error("running the client mod features (now disabled until the game restarts)", e);
        }
    }

    private void OnDestroy()
    {
        InfernoRuntime.Stop();
        _store?.Dispose();
        _harmony?.UnpatchSelf();
    }
}

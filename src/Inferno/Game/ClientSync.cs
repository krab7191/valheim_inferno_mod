using System.Collections.Generic;
using BepInEx.Logging;
using Inferno.Core.Sync;

namespace Inferno.Game;

/// <summary>
/// Client end of the private channel (optional client mod). While connected to an Inferno server, the config
/// entries (and so the F1 ConfigurationManager menu) show the server's settings, and edits are sent to the server
/// as commands. On a server without Inferno nothing happens.
/// </summary>
internal sealed class ClientSync(ConfigSettingsStore store, ManualLogSource log)
{
    private const int MaxHellos = 3;
    private const float HelloIntervalSeconds = 5f;

    // Dragging a slider changes the value every frame; send once the player has stopped for this long.
    private const float EditQuietSeconds = 0.75f;

    private readonly HashSet<string> _editedSections = [];
    private float _sendEditsAt;

    private ZRoutedRpc? _session;
    private int _hellos;
    private float _nextHello;

    /// <summary>True while synced with an Inferno server.</summary>
    public bool IsSynced => store.IsMirroring;

    public void Tick(float now)
    {
        var connected = ZNet.instance != null && !ZNet.instance.IsServer()
            && ZRoutedRpc.instance != null && ZNetScene.instance != null && Player.m_localPlayer != null;

        if (!connected)
        {
            if (_session is not null)
            {
                EndSession();
            }

            return;
        }

        if (_session != ZRoutedRpc.instance)
        {
            BeginSession();
        }

        if (_editedSections.Count > 0 && now >= _sendEditsAt)
        {
            SendEdits();
        }

        if (!IsSynced && _hellos < MaxHellos && now >= _nextHello)
        {
            _hellos++;
            _nextHello = now + HelloIntervalSeconds;
            _session!.InvokeRoutedRPC(SyncServer.HelloRpc, SettingsMessage.ProtocolVersion);
        }
    }

    private void BeginSession()
    {
        if (_session is not null)
        {
            EndSession();
        }

        var discovery = PrefabDiscovery.Run(ZNetScene.instance, log, verbose: false);
        store.BindItems(discovery.Catalog);
        ZRoutedRpc.instance.Register<string>(SyncServer.SettingsRpc, OnSettings);
        store.MenuEdited += OnMenuEdited;
        _session = ZRoutedRpc.instance;
        _hellos = 0;
        _nextHello = 0f;
    }

    private void EndSession()
    {
        store.MenuEdited -= OnMenuEdited;
        _editedSections.Clear();
        store.StopMirroring();
        _session = null;
    }

    private void OnSettings(long sender, string text)
    {
        if (!SettingsMessage.TryDecode(text, out var message))
        {
            log.LogWarning("Received unreadable settings from the server (different Inferno version?); ignoring.");
            return;
        }

        var first = !IsSynced;
        store.ApplyRemote(message);
        if (first)
        {
            log.LogInfo($"Synced Inferno settings from the server ({message.Items.Count} items, {(message.CanEdit ? "editable" : "read-only")}).");
        }
    }

    private void OnMenuEdited(string section)
    {
        _editedSections.Add(section);
        _sendEditsAt = UnityEngine.Time.realtimeSinceStartup + EditQuietSeconds;
    }

    private void SendEdits()
    {
        var sections = new List<string>(_editedSections);
        _editedSections.Clear();
        if (_session is null)
        {
            return;
        }

        foreach (var section in sections)
        {
            foreach (var command in store.CommandsFor(section))
            {
                log.LogInfo($"Sending to server: {command}");
                _session.InvokeRoutedRPC(SyncServer.CommandRpc, command);
            }
        }
    }
}

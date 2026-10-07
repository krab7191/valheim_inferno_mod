using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Inferno.Core.Permissions;
using Inferno.Core.Sync;

namespace Inferno.Game;

/// <summary>
/// Server end of the private channel to players who have the Inferno client mod. Vanilla clients never call these
/// RPCs and ignore anything sent to them, so the channel is invisible to them.
/// </summary>
internal sealed class SyncServer(ConfigSettingsStore store, CommandService commands, ManualLogSource log)
{
    public const string HelloRpc = "Inferno_Hello";
    public const string CommandRpc = "Inferno_Command";
    public const string SettingsRpc = "Inferno_Settings";

    // Many changes in a row (a group command, a config-file save) become one update per interval.
    private const float BroadcastIntervalSeconds = 0.5f;
    private const float HelloCooldownSeconds = 5f;

    private readonly HashSet<long> _subscribers = [];
    private readonly Dictionary<long, float> _lastHello = [];
    private bool _dirty;
    private float _nextBroadcast;

    public void Register(ZRoutedRpc rpc)
    {
        rpc.Register<int>(HelloRpc, OnHello);
        rpc.Register<string>(CommandRpc, OnCommand);
        commands.SettingsChanged += MarkDirty;
    }

    public void Unregister() => commands.SettingsChanged -= MarkDirty;

    /// <summary>Sends pending updates. Call every frame.</summary>
    public void Tick(float now)
    {
        if (!_dirty || now < _nextBroadcast)
        {
            return;
        }

        _dirty = false;
        _nextBroadcast = now + BroadcastIntervalSeconds;
        Broadcast();
    }

    private void MarkDirty() => _dirty = true;

    private void OnHello(long sender, int protocolVersion)
    {
        try
        {
            Hello(sender, protocolVersion);
        }
        catch (Exception e)
        {
            Diagnostics.Error("answering a client mod's hello", e);
        }
    }

    private void Hello(long sender, int protocolVersion)
    {
        var peer = Players.FindPeer(sender);
        var now = UnityEngine.Time.realtimeSinceStartup;
        if (peer is null || (_lastHello.TryGetValue(sender, out var last) && now - last < HelloCooldownSeconds))
        {
            return;
        }

        _lastHello[sender] = now;

        if (protocolVersion != SettingsMessage.ProtocolVersion)
        {
            log.LogWarning($"{Players.FromPeer(peer)} has an incompatible Inferno client (protocol {protocolVersion}, server {SettingsMessage.ProtocolVersion}); menu disabled for them.");
            Players.ShowMessage(peer, ["Your Inferno version doesn't match the server's; the settings menu is disabled. Please update."]);
            return;
        }

        _subscribers.Add(sender);
        log.LogInfo($"{Players.FromPeer(peer)} connected with the Inferno client mod.");
        Send(peer);
    }

    private void OnCommand(long sender, string text)
    {
        try
        {
            commands.HandleMenu(sender, text);
        }
        catch (Exception e)
        {
            Diagnostics.Error("running a settings-menu command", e);
        }
    }

    private void Broadcast()
    {
        foreach (var uid in new List<long>(_subscribers))
        {
            var peer = Players.FindPeer(uid);
            if (peer is null)
            {
                _subscribers.Remove(uid);
                _lastHello.Remove(uid);
                continue;
            }

            try
            {
                Send(peer);
            }
            catch (Exception e)
            {
                Diagnostics.Error("sending settings to a client mod", e);
            }
        }
    }

    private void Send(ZNetPeer peer)
    {
        var sender = Players.FromPeer(peer);
        var canEdit = PermissionPolicy.CanChangeSettings(sender.IsAdmin, store.General.AdminOnly);
        var message = new SettingsMessage(canEdit, store.General, store.Snapshot().Items);
        ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, SettingsRpc, message.Encode());
    }
}

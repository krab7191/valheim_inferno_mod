using System.Collections.Generic;
using Inferno.Core.Commands;

namespace Inferno.Game;

/// <summary>Identifies players from network peers and sends them messages. Server-side only.</summary>
internal static class Players
{
    private const int MaxOnScreenLines = 6;

    public static ZNetPeer? FindPeer(long uid) => uid == 0 ? null : ZNet.instance?.GetPeer(uid);

    public static ZNetPeer? FindPeer(ZRpc rpc)
    {
        var net = ZNet.instance;
        if (net is null)
        {
            return null;
        }

        foreach (var peer in net.GetPeers())
        {
            if (peer.m_rpc == rpc)
            {
                return peer;
            }
        }

        return null;
    }

    /// <summary>
    /// Builds the sender from the server's own connection data. The platform id comes from the network socket,
    /// not from anything the client claims, so admin checks can't be spoofed by a modded client.
    /// </summary>
    public static CommandSender FromPeer(ZNetPeer peer)
    {
        var platformId = peer.m_socket?.GetHostName() ?? "unknown";
        var name = string.IsNullOrEmpty(peer.m_playerName) ? platformId : peer.m_playerName;
        return new CommandSender(name, platformId, ZNet.instance.IsAdmin(platformId));
    }

    /// <summary>Shows lines in the top-left corner of a player's screen (works on vanilla clients).</summary>
    public static void ShowMessage(ZNetPeer peer, IReadOnlyList<string> lines)
    {
        foreach (var line in ReplyLimiter.Limit(lines, MaxOnScreenLines))
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ShowMessage", (int)MessageHud.MessageType.TopLeft, "[Inferno] " + line);
        }
    }

    /// <summary>Prints lines in a player's F5 console (works on vanilla clients).</summary>
    public static void ConsolePrint(ZRpc rpc, IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            ZNet.instance.RemotePrint(rpc, "[Inferno] " + line);
        }
    }
}

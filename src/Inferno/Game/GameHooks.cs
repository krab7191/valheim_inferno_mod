using System;
using HarmonyLib;

namespace Inferno.Game;

/// <summary>
/// Harmony hooks. Each one is a no-op unless Inferno is running on a server (<see cref="InfernoRuntime.Current"/>).
/// Exceptions never escape into the game: a failing hook falls back to vanilla behaviour.
/// </summary>
internal static class GameHooks
{
    private static readonly int SayHash = "Say".GetStableHashCode();
    private static readonly int ChatMessageHash = "ChatMessage".GetStableHashCode();

    /// <summary>
    /// Chat relayed through the server. Normal chat ("Say") and shouts ("ChatMessage") are sent once per recipient
    /// and pass through here. Returning false stops the server from forwarding the line (hides it).
    /// </summary>
    [HarmonyPatch(typeof(ZRoutedRpc), "RouteRPC")]
    private static class ChatRelayPatch
    {
        private static bool Prefix(ZRoutedRpc.RoutedRPCData rpcData)
        {
            var runtime = InfernoRuntime.Current;
            if (runtime is null || (rpcData.m_methodHash != SayHash && rpcData.m_methodHash != ChatMessageHash))
            {
                return true;
            }

            try
            {
                var text = ReadChatText(rpcData);
                if (text is null || !runtime.Commands.HandleChat(rpcData.m_senderPeerID, text))
                {
                    return true;
                }

                return !runtime.Commands.HideCommands;
            }
            catch (Exception e)
            {
                Diagnostics.Error("checking a chat message for commands (message passed through unchanged)", e);
                return true;
            }
        }

        private static string? ReadChatText(ZRoutedRpc.RoutedRPCData rpcData)
        {
            // Read from a copy so the original package is forwarded untouched.
            var pkg = new ZPackage(rpcData.m_parameters.GetArray());
            if (rpcData.m_methodHash == ChatMessageHash)
            {
                pkg.ReadVector3(); // head position
            }

            pkg.ReadInt();    // talker type
            pkg.ReadString(); // UserInfo.Name
            pkg.ReadString(); // UserInfo.UserId
            return pkg.ReadString();
        }
    }

    /// <summary>
    /// F5 console commands forwarded by a client (vanilla forwards built-in server commands such as
    /// <c>listkeys</c>). Inferno handles <c>listkeys fires …</c> itself, with its own permission rules.
    /// </summary>
    [HarmonyPatch(typeof(ZNet), "RPC_RemoteCommand")]
    private static class RemoteCommandPatch
    {
        private static bool Prefix(ZRpc rpc, string command)
        {
            var runtime = InfernoRuntime.Current;
            if (runtime is null)
            {
                return true;
            }

            try
            {
                return !runtime.Commands.HandleConsole(rpc, command);
            }
            catch (Exception e)
            {
                Diagnostics.Error("handling an F5 console command (passed on to the game)", e);
                return true;
            }
        }
    }

    /// <summary>
    /// Server ownership mode: RPCs sent to a fire the server owns would be dropped by vanilla (no instance on the
    /// server). Inferno handles them instead.
    /// </summary>
    [HarmonyPatch(typeof(ZRoutedRpc), "HandleRoutedRPC")]
    private static class OwnedFireRpcPatch
    {
        private static bool Prefix(ZRoutedRpc.RoutedRPCData data)
        {
            var runtime = InfernoRuntime.Current;
            if (runtime is null)
            {
                return true;
            }

            try
            {
                return !runtime.Ownership.TryHandleRpc(data);
            }
            catch (Exception e)
            {
                Diagnostics.Error("handling a player action on a server-owned fire (passed on to the game)", e);
                return true;
            }
        }
    }

    /// <summary>
    /// Server ownership mode: vanilla reassigns owners to nearby players every frame. While this runs, ownership
    /// changes of protected server-owned fires are skipped.
    /// </summary>
    [HarmonyPatch(typeof(ZDOMan), "ReleaseNearbyZDOS")]
    private static class KeepOwnershipPatch
    {
        [ThreadStatic]
        private static bool _active;

        public static bool Active => _active;

        private static void Prefix() => _active = true;

        private static Exception? Finalizer(Exception? __exception)
        {
            _active = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ZDO), nameof(ZDO.SetOwner))]
    private static class SetOwnerPatch
    {
        private static bool Prefix(ZDO __instance)
        {
            if (!KeepOwnershipPatch.Active)
            {
                return true;
            }

            var runtime = InfernoRuntime.Current;
            try
            {
                return runtime is null || !runtime.Ownership.IsProtected(__instance);
            }
            catch (Exception e)
            {
                Diagnostics.Error("keeping a fire server-owned (vanilla owner change allowed)", e);
                return true;
            }
        }
    }

    /// <summary>Before the final world save, undo schedule switch-offs so an uninstall leaves no fire dark.</summary>
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
    private static class ShutdownPatch
    {
        private static void Prefix()
        {
            var runtime = InfernoRuntime.Current;
            if (runtime is null)
            {
                return;
            }

            try
            {
                runtime.Scanner.RestoreForShutdown();
                runtime.Scanner.ReleaseOwnership();
            }
            catch (Exception e)
            {
                Diagnostics.Error("restoring lights at shutdown", e);
            }
        }
    }
}

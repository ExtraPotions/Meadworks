// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// The raids' messages, over the game's routed-RPC bus to each player within 100 m of the host and to nobody else
    /// (features/raids.md section 5): the host's owner names the peers - those of the players it holds and those that
    /// reported in from the raid - and sends each one the text; each client shows it in the middle of its own screen. A
    /// dedicated server has no screen and simply never shows one. Registered on every machine at world start, so a message
    /// never arrives before its handler.
    /// </summary>
    internal static class RaidNet
    {
        private static readonly List<long> Peers = new List<long>();
        private static ZRoutedRpc? _registeredOn;

        /// <summary>Registers the handlers once per routed-RPC bus (a new world builds a new bus).</summary>
        public static void EnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null || ReferenceEquals(bus, _registeredOn))
            {
                return;
            }
            _registeredOn = bus;
            bus.Register<string>(RaidKeys.MessageRpc, OnMessage);
            RaiderOrders.Register(bus);
        }

        /// <summary>Host's owner: shows <paramref name="text"/> to every player within the message range of the raid.</summary>
        public static void Tell(RaidRunner runner, string text)
        {
            EnsureRegistered();
            if (ZRoutedRpc.instance == null)
            {
                return;
            }
            foreach (long peer in PeersNear(runner))
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(peer, RaidKeys.MessageRpc, text);
            }
        }

        /// <summary>
        /// Host's owner: the peers of the players within the message range of the raid - those it holds and those that
        /// reported in - each once, in a list reused by the next call. Also who hears the horn (<see cref="RaidHorn"/>).
        /// </summary>
        public static List<long> PeersNear(RaidRunner runner)
        {
            RaidPlayers.Collect(runner.Position, RaidTable.MessageRange, Peers, out _);
            runner.Heard.AddFresh(Peers, UnityEngine.Time.time); // reporters are within 96 m, so within the 100 too
            return Peers;
        }

        private static void OnMessage(long sender, string text) => Guard.Run("RaidNet.OnMessage", () =>
        {
            if (ZNet.instance != null && sender == (ZNet.instance.IsServer() ? ZNet.GetUID() : ZNet.instance.GetServerPeer()?.m_uid) && MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, text);
            }
        });
    }
}

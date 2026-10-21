// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using ExtraPotions.Wildblood.Adopted.Runtime;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The one message a Screecher's shriek sends each player it deafens, over the game's routed-RPC bus straight to
    /// that player's own machine (the owner of their character), never to everybody: a player's sound and spells exist
    /// only there, and no one else needs to hear of it. A dedicated server only passes it on. Registered once per bus at
    /// world start on every machine (<c>ShriekRpcPatch</c>), keyed on the bus instance so a new world re-registers.
    /// </summary>
    public static class ShriekRpc
    {
        public const string Deafen = "ecr_deafen";

        private static ZRoutedRpc? _registeredOn;

        /// <summary>Registers the handler on the current bus, once per bus; safe to call from anywhere.</summary>
        public static void EnsureRegistered() => TryEnsureRegistered();

        public static bool TryEnsureRegistered()
        {
            ZRoutedRpc bus = ZRoutedRpc.instance;
            if (bus == null)
            {
                return false;
            }
            if (!NativeRpcOwnership.Routed(bus, NativeRpcOwner.Shriek, () => {
                bus.Register<float>(Deafen, OnDeafen);
            }, () => { if (ReferenceEquals(_registeredOn, bus)) _registeredOn = null; }, Deafen)) return false;
            _registeredOn = bus;
            return true;
        }

        /// <summary>Creature-owner side: deafen <paramref name="player"/> for <paramref name="seconds"/> on their own machine.</summary>
        public static void Send(Player player, float seconds)
        {
            if (!TryEnsureRegistered()) return;
            long peer = player.GetOwner();
            if (ZRoutedRpc.instance == null || peer == ZRoutedRpc.Everybody)
            {
                return; // a character with no owner yet: 0 would address the whole server, so it is skipped
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(peer, Deafen, seconds);
        }

        private static void OnDeafen(long sender, float seconds) =>
            Guard.Run("ShriekRpc.Deafen", () => ShriekDeafness.Apply(Player.m_localPlayer, seconds));
    }
}

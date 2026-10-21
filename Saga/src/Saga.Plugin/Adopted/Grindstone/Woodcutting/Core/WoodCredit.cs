// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/WoodCredit.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>What a <see cref="WoodCredit"/> is for.</summary>
    public enum WoodCreditKind
    {
        /// <summary>A tree the woodcutter felled, by a swing or by a chain.</summary>
        Fell = 0,

        /// <summary>A log the woodcutter broke into wood.</summary>
        Split = 1,
    }

    /// <summary>
    /// Woodcutting credit for something that happened on another machine: a tree felled or a log broken on its owner.
    /// global::Skills live on each player's own client, so the owner sends the credit by routed RPC (<see cref="Keys.RpcWoodCredit"/>)
    /// to that player's machine alone (<see cref="PlayerIds.PeerOf"/>, Everybody when it cannot tell), and only the client
    /// whose local player has that player ID acts on it (as <see cref="CookCredit"/>);
    /// a dedicated server has no local player and ignores it. An offline woodcutter loses it. The receiver hands it to
    /// experience (<see cref="WoodXp.OnCredit"/>: amount, discovery) and to <see cref="Domino"/> (the chain message).
    /// The routed RPC is registered in a ZNet.Awake postfix, where the game creates a fresh ZRoutedRpc for each session.
    /// </summary>
    public static class WoodCredit
    {
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class NetAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => ZRoutedRpc.instance?.Register<ZPackage>(Keys.RpcWoodCredit, Receive);
        }

        /// <summary>
        /// Sends credit to the player with this ID, wherever they are. <paramref name="species"/> is the tree kind
        /// (<see cref="FellContext.Species"/>) or the log prefab; <paramref name="amount"/> the experience before the
        /// receiver's multiplier and discovery bonus; <paramref name="chain"/> the domino depth (0 for a swing).
        /// </summary>
        public static void Send(long playerId, WoodCreditKind kind, string species, float amount, int chain)
        {
            if (playerId == 0L || ZRoutedRpc.instance == null)
                return;
            ZPackage pkg = new ZPackage();
            pkg.Write(playerId);
            pkg.Write((int)kind);
            pkg.Write(species ?? "");
            pkg.Write(amount);
            pkg.Write(chain);
            ZRoutedRpc.instance.InvokeRoutedRPC(PlayerIds.PeerOf(playerId), Keys.RpcWoodCredit, pkg);
        }

        private static void Receive(long sender, ZPackage pkg)
        {
            long playerId = pkg.ReadLong();
            WoodCreditKind kind = (WoodCreditKind)pkg.ReadInt();
            string species = pkg.ReadString();
            float amount = pkg.ReadSingle();
            int chain = pkg.ReadInt();
            Player player = Player.m_localPlayer;
            if (player == null || player.GetPlayerID() != playerId || !WoodSkill.Active)
                return;
            HookGuard.Run("wood credit", () => WoodXp.OnCredit(player, kind, species, amount));
            HookGuard.Run("domino credit", () => Domino.OnCredit(player, kind, chain));
        }
    }
}

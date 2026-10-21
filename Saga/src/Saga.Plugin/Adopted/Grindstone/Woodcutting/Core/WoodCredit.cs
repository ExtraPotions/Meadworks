// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/WoodCredit.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Saga.Runtime;
using ExtraPotions.Saga.Skills;
using HarmonyLib;
using UnityEngine;

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
    /// Woodcutting credit for a fell or a split. The owner of the tree or log reports it to the server
    /// (<see cref="Keys.RpcWoodReport"/>). The server checks that this sender owns that ZDO, that the prefab matches
    /// the kind, and that the amount is within the configured cap, then sends <see cref="Keys.RpcWoodCredit"/> itself.
    /// A client applies a credit only when the sender is the server, and clamps the amount again. Skills live on each
    /// player's own client. A dedicated server has no local player and does not raise the skill itself. An offline
    /// woodcutter loses the credit. The receiver hands it to <see cref="WoodXp.OnCredit"/> and to <see cref="Domino"/>.
    /// Both routed RPCs are registered in a ZNet.Awake postfix, where the game creates a fresh ZRoutedRpc per session.
    /// </summary>
    public static class WoodCredit
    {
        static readonly HashSet<ZDOID> credited = new HashSet<ZDOID>();

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class NetAwake
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                credited.Clear();
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(Keys.RpcWoodReport, OnReport);
                ZRoutedRpc.instance.Register<ZPackage>(Keys.RpcWoodCredit, Receive);
            }
        }

        /// <summary>
        /// Reports a fell or a split from the owner of <paramref name="source"/>. <paramref name="species"/> is the tree
        /// kind the owner saw; the server replaces it from the prefab. <paramref name="amount"/> is the experience
        /// before the receiver's multiplier and discovery bonus. <paramref name="chain"/> is the domino depth.
        /// </summary>
        public static void Send(long playerId, WoodCreditKind kind, string species, float amount, int chain, ZDOID source)
        {
            if (playerId == 0L || source.IsNone() || ZRoutedRpc.instance == null || ZNet.instance == null || ZDOMan.instance == null)
                return;
            ZDO zdo = ZDOMan.instance.GetZDO(source);
            if (zdo == null || !zdo.IsValid() || zdo.GetOwner() != ZNet.GetUID())
                return;
            if (ZNet.instance.IsServer())
                Accept(ZNet.GetUID(), playerId, kind, amount, chain, source);
            else
            {
                long server = ZNet.instance.GetServerPeer()?.m_uid ?? 0L;
                if (server == 0L) return;
                ZPackage pkg = new ZPackage();
                pkg.Write(playerId);
                pkg.Write((int)kind);
                pkg.Write(species ?? "");
                pkg.Write(amount);
                pkg.Write(chain);
                pkg.Write(source);
                ZRoutedRpc.instance.InvokeRoutedRPC(server, Keys.RpcWoodReport, pkg);
            }
        }

        private static void OnReport(long sender, ZPackage pkg)
        {
            try
            {
                long playerId = pkg.ReadLong();
                var kind = (WoodCreditKind)pkg.ReadInt();
                pkg.ReadString();
                float amount = pkg.ReadSingle();
                int chain = pkg.ReadInt();
                ZDOID source = pkg.ReadZDOID();
                Accept(sender, playerId, kind, amount, chain, source);
            }
            catch (Exception error) { NativeSaga.Report(error, "wood credit"); }
        }

        private static void Accept(long sender, long playerId, WoodCreditKind kind, float amount, int chain, ZDOID source)
        {
            bool server = ZNet.instance != null && ZNet.instance.IsServer();
            if (!server || playerId == 0L || source.IsNone() || credited.Contains(source))
                return;
            ZDO zdo = ZDOMan.instance != null ? ZDOMan.instance.GetZDO(source) : null;
            bool owns = zdo != null && zdo.IsValid() && zdo.GetOwner() == sender;
            GameObject prefab = zdo != null && ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
            bool isTree = prefab != null && prefab.GetComponent<TreeBase>() != null;
            bool isLog = prefab != null && prefab.GetComponent<TreeLog>() != null;
            bool fell = kind == WoodCreditKind.Fell;
            bool knownKind = kind == WoodCreditKind.Fell || kind == WoodCreditKind.Split;
            bool kindOk = knownKind && WoodCreditRules.KindMatches(isTree, isLog, fell);
            if (!WoodCreditRules.ServerAccepts(server, owns, kindOk, true))
                return;
            credited.Add(source);
            WoodValues values = CurrentValues();
            float clamped = WoodCreditRules.Clamp(amount, WoodCreditRules.Cap(values, fell));
            int maxChain = DominoSettings.MaxChain != null ? DominoSettings.MaxChain.Value : 50;
            int clampedChain = WoodCreditRules.ClampChain(chain, maxChain);
            Deliver(playerId, kind, SpeciesOf(prefab, fell), clamped, clampedChain);
        }

        private static void Deliver(long playerId, WoodCreditKind kind, string species, float amount, int chain)
        {
            if (ZRoutedRpc.instance == null) return;
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
            try { Apply(sender, pkg); }
            catch (Exception error) { NativeSaga.Report(error, "wood credit"); }
        }

        private static void Apply(long sender, ZPackage pkg)
        {
            long playerId = pkg.ReadLong();
            var kind = (WoodCreditKind)pkg.ReadInt();
            string species = pkg.ReadString();
            float amount = pkg.ReadSingle();
            int chain = pkg.ReadInt();
            if (!WoodCreditRules.ClientAccepts(sender == ServerPeer()))
                return;
            bool fell = kind == WoodCreditKind.Fell;
            if (!fell && kind != WoodCreditKind.Split)
                return;
            amount = WoodCreditRules.Clamp(amount, WoodCreditRules.Cap(CurrentValues(), fell));
            chain = WoodCreditRules.ClampChain(chain, DominoSettings.MaxChain != null ? DominoSettings.MaxChain.Value : 50);
            Player player = Player.m_localPlayer;
            if (player == null || player.GetPlayerID() != playerId || !WoodSkill.Active)
                return;
            HookGuard.Run("wood credit", () => WoodXp.OnCredit(player, kind, species, amount));
            HookGuard.Run("domino credit", () => Domino.OnCredit(player, kind, chain));
        }

        static WoodValues CurrentValues()
        {
            WoodValues values = WoodValues.Defaults();
            if (WoodExperienceSettings.FellExperience == null) return values;
            values.Fell = WoodExperienceSettings.FellExperience.Value;
            values.Split = WoodExperienceSettings.SplitExperience.Value;
            values.TierScaling = WoodExperienceSettings.TierScaling.Value;
            values.TierExperience = WoodExperienceSettings.TierExperience.Value;
            return values;
        }

        static string SpeciesOf(GameObject prefab, bool fell)
        {
            if (prefab == null) return "";
            if (fell)
            {
                TreeBase tree = prefab.GetComponent<TreeBase>();
                if (tree != null && tree.m_logPrefab != null) return tree.m_logPrefab.name;
            }
            return Utils.GetPrefabName(prefab);
        }

        static long ServerPeer()
        {
            if (ZNet.instance == null) return 0L;
            if (ZNet.instance.IsServer()) return ZNet.GetUID();
            var peer = ZNet.instance.GetServerPeer();
            return peer != null ? peer.m_uid : 0L;
        }
    }
}

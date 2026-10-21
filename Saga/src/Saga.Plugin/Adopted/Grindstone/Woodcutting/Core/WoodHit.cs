// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Core/WoodHit.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// How a hit on wood carries its woodcutter to the target's owner. Damage(HitData) on a tree, log or stump runs on
    /// the hitting machine and sends the hit to the owner by RPC; the prefixes here fill in, before it goes, fields
    /// HitData already sends and that trees, logs and stumps never read:
    /// <list type="bullet">
    /// <item>m_skill: WoodCutting. The game sets it for an axe swing at wood; it is set here for a log's impact.</item>
    /// <item>m_skillLevel: the woodcutter's Woodcutting level. The game writes the weapon's own skill level there (Axes);
    /// only Character reads it, for status-effect levels.</item>
    /// <item>m_attacker: the woodcutter's player. The game's own for a swing; for an impact, the log's woodcutter when
    /// that player is loaded on the log's owner (otherwise none, and the chain is anonymous).</item>
    /// <item>m_skillRaiseAmount: on an impact (<see cref="IsImpact"/>: hit type Tree, which every log uses) only, the
    /// chain depth: 1 for the first tree a felled log hits. Swings keep the game's value.</item>
    /// </list>
    /// A swing is tagged on the swinging client and recorded in its <see cref="SwingScope"/>. An impact on other wood is
    /// tagged while an <see cref="ImpactScope"/> is open on the log's owner, then handed to <see cref="Domino.OnImpact"/>,
    /// which may change its damage.
    /// </summary>
    public static class WoodHit
    {
        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        private static class TreeHit
        {
            [HarmonyPrefix]
            private static void Prefix(TreeBase __instance, HitData hit) => Tag(__instance, hit);
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        private static class LogHit
        {
            [HarmonyPrefix]
            private static void Prefix(TreeLog __instance, HitData hit) => Tag(__instance, hit);
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        private static class StumpHit
        {
            [HarmonyPrefix]
            private static void Prefix(Destructible __instance, HitData hit)
            {
                if (WoodSkill.IsWood(__instance))
                    Tag(__instance, hit);
            }
        }

        /// <summary>
        /// Whether a hit is a falling log's impact. Every log's and log half's ImpactEffect uses hit type Tree (13), read
        /// from the prefabs; Impact is only the component's default, kept for modded logs.
        /// </summary>
        public static bool IsImpact(HitData hit) =>
            hit.m_hitType == HitData.HitType.Tree || hit.m_hitType == HitData.HitType.Impact;

        /// <summary>The chain depth a hit carries: 0 for a swing, 1 or more for a felled log's impact.</summary>
        public static int Chain(HitData hit) =>
            IsImpact(hit) ? Mathf.Max(1, Mathf.RoundToInt(hit.m_skillRaiseAmount)) : 0;

        /// <summary>The player ID behind a hit's attacker, read from the attacker's ZDO; 0 when unknown.</summary>
        public static long PlayerId(HitData hit) => PlayerIds.Of(hit.m_attacker);

        /// <summary>Hits that are neither a woodcutting hit nor inside an impact scope (fire, creatures) are left alone at once.</summary>
        private static void Tag(Component target, HitData hit)
        {
            if (hit != null && WoodSkill.Active && (ImpactScope.Current != null || hit.m_skill == WoodSkill.Skill))
                HookGuard.Run("wood hit", static wood => TagHit(wood.target, wood.hit), (target, hit));
        }

        private static void TagHit(Component target, HitData hit)
        {
            Woodcutter log = ImpactScope.Current;
            if (log != null && IsImpact(hit))
            {
                // A log's impact on itself (ImpactEffect.m_damageToSelf) stays the game's own.
                if (target != ImpactScope.Log)
                    TagImpact(hit, log);
            }
            else if (hit.m_skill == WoodSkill.Skill && IsLocalSwing(hit))
                TagSwing(target, hit);
        }

        private static void TagSwing(Component target, HitData hit)
        {
            hit.m_skillLevel = WoodSkill.Local();
            SwingScope.Record(target);
        }

        private static void TagImpact(HitData hit, Woodcutter log)
        {
            int chain = log.Chain + 1;
            hit.m_skill = WoodSkill.Skill;
            hit.m_skillLevel = log.Level;
            hit.m_skillRaiseAmount = chain;
            hit.m_attacker = PlayerZdoid(log.PlayerId);
            HookGuard.Run("domino", () => Domino.OnImpact(hit, log, chain));
        }

        private static bool IsLocalSwing(HitData hit)
        {
            Player player = Player.m_localPlayer;
            return player != null && !IsImpact(hit) && hit.GetAttacker() == player;
        }

        /// <summary>The ZDOID of the loaded player with this player ID, or none.</summary>
        private static ZDOID PlayerZdoid(long playerId)
        {
            if (playerId == 0L)
                return ZDOID.None;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player != null && player.GetPlayerID() == playerId)
                    return player.GetZDOID();
            }
            return ZDOID.None;
        }
    }
}

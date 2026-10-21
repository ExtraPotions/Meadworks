// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/CleanSplits/CleanSplits.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Clean splits: an axe swing at a log can split it at once, and a cleanly split log gives more wood.
    /// <list type="bullet">
    /// <item>TreeLog.RPC_Damage handles every hit on the log's ZDO owner, in this order: it reads the remaining health
    /// from the ZDO (ZDOVars.s_health) and stops at 0; keeps a copy of the hit (HitData.Clone) for TreeLog.Destroy;
    /// applies the log's resistances (m_damages, HitData.ApplyResistance) and takes the total (GetTotalDamage); refuses
    /// a hit below the log's tool tier (CheckToolTier(m_minToolTier, true)); pushes the log; shows the total as damage
    /// text; stops when the total is 0; writes the health less the total back; and at 0 calls Destroy with the copy.</item>
    /// <item><see cref="LogBreaking"/> shows every woodcutting hit to <see cref="OnLogHit"/> before that. A swing the
    /// game will let through and that hurts the log rolls the swinger's chance. A clean split raises the hit's damage,
    /// every type by the same factor, until its total after resistances is the remaining health (a hit that already
    /// finishes the log keeps its damage). The game then breaks the log, and its damage text shows that health, in the
    /// same colour as before (a uniform raise keeps the resistance category). The split marks the log's ZDO
    /// (<see cref="Keys.CleanSplit"/>) and calls "Clean split!" out at the hit.</item>
    /// <item>The copy handed to Destroy is taken after this, so it carries the raised damage. Destroy reads only which
    /// damage type dominates it (Game.CheckDropConversion), and a uniform raise keeps that.</item>
    /// <item>A whole log drops nothing itself and splits into two halves; <see cref="LogSpawns"/> gives both the mark.
    /// When a marked half breaks into wood, <see cref="Bonus"/> adds to its drop count.</item>
    /// </list>
    /// Everything runs on the log's owner, whoever swung: the swinger's level travels in the hit (<see cref="WoodHit"/>),
    /// the mark in the log's ZDO, and the callout to every client near the log.
    /// </summary>
    public static class CleanSplits
    {
        public static string Callout => SagaText.Packet("gs_f1994596ac0aab5b");

        /// <summary>
        /// Damage on top of the remaining health, so float rounding in the game's own sum never leaves a sliver of
        /// health. Too small to show: the damage text rounds to one decimal.
        /// </summary>
        private const float Headroom = 0.01f;

        private static readonly int CleanHash = Keys.CleanSplit.GetStableHashCode();

        /// <summary>
        /// Called by <see cref="LogBreaking"/> on the log's owner for every woodcutting hit on a log, before the game
        /// applies it (TreeLog.RPC_Damage prefix). A clean split raises the hit to finish the log and marks the log.
        /// </summary>
        public static void OnLogHit(TreeLog log, HitData hit, Woodcutter woodcutter)
        {
            if (!IsSwing(hit, woodcutter) || !hit.CheckToolTier(log.m_minToolTier, alwaysAllowTierZero: true))
                return;
            ZDO zdo = log.m_nview.GetZDO();
            float health = zdo.GetFloat(ZDOVars.s_health);
            float multiplier = health > 0f ? Multiplier(log, hit, health) : 0f;
            if (multiplier <= 0f || !Rolls(woodcutter.Level))
                return;
            if (multiplier > 1f)
                hit.m_damage.Modify(multiplier);
            zdo.Set(CleanHash, true);
            WoodCallout.Broadcast(hit.m_point, Callout);
        }

        /// <summary>
        /// Called by <see cref="LogBreaking"/> before the drops spawn: the clean split bonus for a marked log that drops
        /// wood (a half), 0 otherwise. A whole log drops nothing itself; its halves inherit the mark.
        /// </summary>
        public static float Bonus(BreakContext broken) =>
            broken.Clean && broken.DropsWood ? CleanSplitSettings.WoodBonus.Value / 100f : 0f;

        /// <summary>Called by <see cref="LogBreaking"/> on the log's owner after the log broke. Clean splits need nothing here.</summary>
        public static void OnLogBroken(BreakContext broken)
        {
        }

        /// <summary>A swing; a felled log's impact on another log is Domino's.</summary>
        private static bool IsSwing(HitData hit, Woodcutter woodcutter) =>
            woodcutter.Chain == 0 && !WoodHit.IsImpact(hit);

        /// <summary>
        /// The factor on every damage type that makes the hit, after the log's resistances, take the remaining health
        /// plus <see cref="Headroom"/>: above 1 to raise it, at most 1 when it already finishes the log, 0 when the
        /// resistances leave it no damage. Worked out on a copy. The game's total adds a flat world-level bonus for
        /// creature attackers only, which could only add to this; it is left out.
        /// </summary>
        private static float Multiplier(TreeLog log, HitData hit, float health)
        {
            HitData resisted = hit.Clone();
            resisted.ApplyResistance(log.m_damages, out _);
            float damage = resisted.m_damage.GetTotalDamage();
            return damage > 0f ? (health + Headroom) / damage : 0f;
        }

        /// <summary>Rolls the clean split chance at a woodcutter's level: the setting's percent at 100, linear from 0.</summary>
        private static bool Rolls(float level)
        {
            float chance = WoodSkill.Share(CleanSplitSettings.ChanceAt100.Value, level);
            return chance > 0f && Random.value <= chance;
        }
    }
}

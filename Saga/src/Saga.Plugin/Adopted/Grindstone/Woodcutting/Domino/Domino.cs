// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Domino/Domino.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Domino felling: a felled log hits other wood harder, so one tree can knock over the next, and the woodcutter is
    /// told how long the chain got.
    /// <list type="bullet">
    /// <item><b>A log's impact in the game.</b> ImpactEffect.OnCollisionEnter runs on the log's ZDO owner. For a
    /// struck object whose collider layer is in m_triggerMask (Default, where every tree trunk, log and stump sits, is),
    /// at a speed of at least m_minVelocity, it builds a hit from m_damages times Utils.LerpStep(m_minVelocity,
    /// m_maxVelocity, speed), with m_toolTier and m_hitType, and calls Damage on it; then no impact for m_interval
    /// seconds. Every log and log half has blunt 50 and chop 30 (Ashlands logs blunt 110, chop 40, fire 20), tool tier
    /// 2, full damage from 5 m/s (Ashlands whole logs 7), one impact per 0.25 s and hit type Tree (13).</item>
    /// <item><b>What lands.</b> TreeBase, TreeLog and the tree-type Destructibles are immune to every damage type but
    /// chop (their m_damageModifiers / m_damages), so only the chop part counts: at most 30 per impact against 80
    /// (beech, fir, birch, swamp), 120 (pine) or 200 (oak, big firs, Ashlands) health. RPC_Damage checks the tool tier
    /// with HitData.CheckToolTier: tier 2 reaches every tree but the Mistlands' Yggdrasil shoots (m_minToolTier 4).</item>
    /// <item><b>Harder impacts.</b> <see cref="OnImpact"/> multiplies every damage type of the hit by 1 + the feller's
    /// share of Domino Impact At 100 while the chain is no deeper than Domino Max Chain; the target's resistances then
    /// keep the chop part, so at level 100 (×3) one fast impact fells a beech. All types are scaled alike, so the
    /// majority type the game's drop conversions read (Game.CheckDropConversion) stays the same.</item>
    /// <item><b>Tool tier.</b> A boosted impact hits with the log's own tier (TreeLog.m_minToolTier) when that is
    /// higher, so a Yggdrasil log can knock over the next shoot; every other log keeps tier 2.</item>
    /// <item><b>Chain message.</b> A tree a felled log knocks over counts as the woodcutter's fell; its credit carries
    /// the chain depth (<see cref="WoodCredit"/>), and <see cref="OnCredit"/> shows "Chain ×N!" on the woodcutter's
    /// screen, N counting the tree felled by the swing.</item>
    /// </list>
    /// Players, creatures and buildings never reach this: <see cref="WoodHit"/> hands only impacts on wood to it.
    /// </summary>
    public static class Domino
    {
        /// <summary>
        /// Called by <see cref="WoodHit"/> on the log's owner for each impact of a felled log on other wood (a tree, a
        /// log, a tree-type Destructible), after the hit was tagged with the log's woodcutter and before it is sent to
        /// the target's owner. <paramref name="chain"/> is the depth the hit carries (1 = the first tree a felled log
        /// hits). Beyond Domino Max Chain, or at level 0, the hit keeps the game's damage and tier.
        /// </summary>
        public static void OnImpact(HitData hit, Woodcutter log, int chain)
        {
            float bonus = Bonus(log, chain);
            if (hit == null || bonus <= 0f)
                return;
            hit.m_damage.Modify(1f + bonus);
            hit.m_toolTier = (short)Mathf.Max(hit.m_toolTier, LogTier());
        }

        /// <summary>
        /// Called by <see cref="WoodCredit"/> on the woodcutter's own client for every credit they receive. A fell
        /// deeper than 0 was a domino: the chain so far is shown in the centre of the screen.
        /// </summary>
        public static void OnCredit(Player player, WoodCreditKind kind, int chain)
        {
            if (player == null || kind != WoodCreditKind.Fell || chain < 1 || !WoodSkill.Active)
                return;
            player.Message(MessageHud.MessageType.Center, SagaText.Format("gs_6b090cffdb55129b", chain + 1));
        }

        /// <summary>The extra impact damage for this woodcutter at this depth: 0 for the game's own, 2 for three times.</summary>
        private static float Bonus(Woodcutter log, int chain)
        {
            if (!WoodSkill.Active || log == null || chain < 1 || chain > DominoSettings.MaxChain.Value)
                return 0f;
            return WoodSkill.Share(DominoSettings.ImpactAt100.Value, log.Level);
        }

        /// <summary>The tool tier of the log whose impact is being handled; 0 outside an <see cref="ImpactScope"/>.</summary>
        private static int LogTier()
        {
            TreeLog log = ImpactScope.Log;
            return log != null ? log.m_minToolTier : 0;
        }
    }
}

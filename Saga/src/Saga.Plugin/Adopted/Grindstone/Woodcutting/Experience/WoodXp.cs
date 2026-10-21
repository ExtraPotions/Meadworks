// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Experience/WoodXp.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Woodcutting experience, on top of the game's own.
    /// <list type="bullet">
    /// <item><b>Swings.</b> Attack.DoMeleeAttack runs on the attacker's client and, once per swing that hit any wood,
    /// calls Character.RaiseSkill(WoodCutting, m_raiseSkillAmount, ×1.5 if the swing also hit a creature). For a
    /// player that is Player.RaiseSkill, which multiplies by the status effects' SEMan.ModifyRaiseSkill (Rested +50%)
    /// and calls global::Skills.RaiseSkill. <see cref="SwingScope"/>'s prefix there multiplies the factor by
    /// <see cref="SwingScale"/>. DoMeleeAttack counts a hit on wood whether or not it does damage (the tool tier is
    /// checked in the target's RPC_Damage, on its owner), so a swing at wood too hard for the axe still trains the
    /// skill; it gets the game's amount but no tier bonus, or such wood would be a faster endless farm.</item>
    /// <item><b>Fells and splits.</b> A tree falls on its owner and a log breaks on its owner, which may be another
    /// machine. The owner reports that ZDO to the server, the server sends the woodcutter a <see cref="WoodCredit"/>,
    /// and <see cref="OnCredit"/> raises it on the woodcutter's own client through <see cref="SwingScope.RaiseUnscoped"/>,
    /// so an open swing scope never scales it again. It still goes through Player.RaiseSkill (Rested applies) and
    /// global::Skills.Skill.Raise, which multiplies every raise by the world's skill-gain rate (Game.m_skillGainRate).</item>
    /// <item><b>Discovery.</b> The first fell of each kind of tree is multiplied (<see cref="WoodDiscovery"/>).</item>
    /// </list>
    /// </summary>
    public static class WoodXp
    {
        /// <summary>The Experience Multiplier, never below 0.</summary>
        public static float Multiplier => Mathf.Max(0f, WoodExperienceSettings.Multiplier.Value);

        private static float SmallWoodShare => Mathf.Clamp01(WoodExperienceSettings.SmallWoodExperience.Value / 100f);

        /// <summary>
        /// Called by <see cref="SwingScope"/> on the swinging client when the game raises Woodcutting for a swing that
        /// hit wood: the multiplier for that raise. <paramref name="hardest"/> is the hardest wood the swing hit.
        /// The multiplier, times the wood's tier factor when the axe can cut it, times the small-wood share when it
        /// has less health than Small Wood Health.
        /// </summary>
        public static float SwingScale(WoodTarget hardest)
        {
            if (hardest == null)
                return Multiplier;
            float scale = Multiplier * (CanCut(hardest) ? TierFactor(hardest.Tier) : 1f);
            return hardest.Health < WoodExperienceSettings.SmallWoodHealth.Value ? scale * SmallWoodShare : scale;
        }

        /// <summary>
        /// The experience factor of wood that needs this tool tier: 1 + Tier Experience Per Tool Tier per tier (birch
        /// and oak need 2, Yggdrasil 4), or 1 while Tier Scaling is off.
        /// </summary>
        public static float TierFactor(int tier)
        {
            if (!WoodExperienceSettings.TierScaling.Value)
                return 1f;
            return 1f + Mathf.Max(0f, WoodExperienceSettings.TierExperience.Value) / 100f * Mathf.Max(0, tier);
        }

        /// <summary>Called by <see cref="Felling"/> on the tree's owner, last of the felling features.</summary>
        public static void OnFelled(FellContext fell)
        {
            Woodcutter woodcutter = fell.Woodcutter;
            if (woodcutter == null || woodcutter.PlayerId == 0L)
                return;
            // Sent even when the amount is 0: the same credit carries the domino chain message.
            float amount = Mathf.Max(0f, WoodExperienceSettings.FellExperience.Value) * TierFactor(fell.Tree.m_minToolTier);
            WoodCredit.Send(woodcutter.PlayerId, WoodCreditKind.Fell, fell.Species, amount, woodcutter.Chain, Source(fell.Tree != null ? fell.Tree.m_nview : null));
        }

        /// <summary>
        /// Called by <see cref="LogBreaking"/> on the log's owner before Destroy removes the log. Only a break that drops wood (a
        /// half, or a log without halves) earns split experience, for the woodcutter whose hit broke it: a swing, or a
        /// felled log's impact. A log that breaks on its own landing, by fire or by a creature has no breaker.
        /// </summary>
        public static void OnLogBroken(BreakContext broken)
        {
            Woodcutter breaker = broken.Breaker;
            if (!broken.DropsWood || breaker == null || breaker.PlayerId == 0L)
                return;
            float amount = Mathf.Max(0f, WoodExperienceSettings.SplitExperience.Value) * TierFactor(broken.Log.m_minToolTier);
            if (amount > 0f)
                WoodCredit.Send(breaker.PlayerId, WoodCreditKind.Split, broken.LogPrefab, amount, 0, Source(broken.Log != null ? broken.Log.m_nview : null));
        }

        static ZDOID Source(ZNetView view)
        {
            if (view == null || !view.IsValid()) return ZDOID.None;
            ZDO zdo = view.GetZDO();
            return zdo == null ? ZDOID.None : zdo.m_uid;
        }

        /// <summary>
        /// Called by <see cref="WoodCredit"/> on the woodcutter's own client for every credit they receive: the amount
        /// times the Experience Multiplier, times the Discovery Multiplier for the character's first fell of the kind
        /// of tree. Discovery is recorded only when the credit earns something, so it is not spent while fell
        /// experience is off.
        /// </summary>
        public static void OnCredit(Player player, WoodCreditKind kind, string species, float amount)
        {
            float total = Mathf.Max(0f, amount) * Multiplier;
            if (player == null || total <= 0f)
                return;
            if (kind == WoodCreditKind.Fell && WoodDiscovery.TryRecord(player, species))
                total *= Mathf.Max(1f, WoodExperienceSettings.DiscoveryMultiplier.Value);
            SwingScope.RaiseUnscoped(player, total);
        }

        /// <summary>
        /// Whether the swinging axe's tool tier reaches the wood's (the check TreeBase, TreeLog and Destructible make in
        /// RPC_Damage through HitData.CheckToolTier; tier 0 wood is always allowed). True when the weapon is unknown.
        /// </summary>
        private static bool CanCut(WoodTarget wood)
        {
            Attack attack = SwingScope.Attack;
            ItemDrop.ItemData weapon = attack != null ? attack.m_weapon : null;
            return wood.Tier <= 0 || weapon == null || weapon.m_shared.m_toolTier >= wood.Tier;
        }
    }
}

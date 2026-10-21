// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The situational movement channels of the aggregate's <c>ModifySpeed</c>: sprinting, encumbered, the window after
    /// a dodge and paved ground (sneaking is Phase 1's, in <see cref="EcfAggregate"/>). Called by the game on the local
    /// player's own client, every movement update: float reads and cached flags only.
    /// </summary>
    internal static class SpeedBonuses
    {
        /// <summary>The fraction of the base speed to add now (0 when nothing applies).</summary>
        public static float Situational(AggregateValues v, Character character)
        {
            float add = 0f;
            float sneak = v[EffectKind.MoveSpeedSneak];
            if (sneak != 0f && character.IsCrouching())
            {
                add += sneak;
            }
            float sprint = v[EffectKind.MoveSpeedSprint];
            if (sprint != 0f && character.IsRunning())
            {
                add += sprint;
            }
            return add + Conditional(v, character);
        }

        private static float Conditional(AggregateValues v, Character character)
        {
            float add = 0f;
            float heavy = v[EffectKind.MoveSpeedEncumbered];
            if (heavy != 0f && character.IsEncumbered())
            {
                add += heavy;
            }
            if (CombatWindows.MomentumActive)
            {
                add += v[EffectKind.MoveSpeedAfterDodge];
            }
            if (PathGround.OnPath)
            {
                add += v[EffectKind.MoveSpeedPaved];
            }
            return add;
        }
    }

    /// <summary>
    /// The Phase 2 parts of the aggregate's <c>ModifyAttack</c>: sneak-attack multiplier, stagger power, Fafnir's Greed
    /// and Evader's Fury. Runs on the attacker's own client while the hit is built, so the numbers travel in the hit.
    /// </summary>
    internal static class AttackBonuses
    {
        /// <summary>AFX-15: stacks counted per full 999 coins, stacks past the second count half, at most 5 stacks.</summary>
        private const int CoinsPerStack = 999, FullStacks = 2, MaxStacks = 5;

        /// <summary>Effective coin stacks carried, refreshed on rebuild (inventory changes rebuild), never per hit.</summary>
        public static float CoinStacks { get; private set; }

        public static void Apply(AggregateValues v, ref HitData hit)
        {
            float surprise = v[EffectKind.SurpriseBonus];
            if (surprise != 0f && hit.m_backstabBonus > 1f)
            {
                hit.m_backstabBonus *= 1f + surprise;
            }
            float stagger = v[EffectKind.StaggerPower];
            if (stagger != 0f)
            {
                hit.m_staggerMultiplier *= 1f + stagger;
            }
            float bonus = v[EffectKind.CoinDamage] * CoinStacks;
            if (CombatWindows.FuryActive)
            {
                bonus += v[EffectKind.DodgeFury];
            }
            if (bonus != 0f)
            {
                hit.m_damage.Modify(1f + bonus);
            }
        }

        /// <summary>Rebuild: count the coins once. The game's own coin reference is the store's coin prefab.</summary>
        public static void RefreshCoins(Player player, bool wanted)
        {
            CoinStacks = 0f;
            ItemDrop? coin = StoreGui.instance != null ? StoreGui.instance.m_coinPrefab : null;
            if (!wanted || coin == null)
            {
                return;
            }
            int stacks = Math.Min(MaxStacks, player.GetInventory().CountItems(coin.m_itemData.m_shared.m_name) / CoinsPerStack);
            CoinStacks = Math.Min(stacks, FullStacks) + Math.Max(0, stacks - FullStacks) * 0.5f;
        }
    }
}

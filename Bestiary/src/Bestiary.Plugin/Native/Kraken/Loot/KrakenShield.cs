// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The Kraken shield: fine wood planks and silver round the kraken's beak. A copy of the game's silver shield (its
    /// block sounds and effects, its place on the arm and on the back) wearing the workshop's shield, laid as the silver
    /// shield's model lies in the hand (front out, up the arm) with its one grip, behind the middle of the board, where the
    /// hand is. Better than the serpent scale shield on every count, and it parries, which the serpent's cannot: a parried
    /// creature is bitten (<see cref="ParryBite"/>).
    /// <para>
    /// Serpent scale shield, for comparison: block 60 (+6 a level), deflection 100 (+5), no parry, -10% movement,
    /// durability 250 (+50), weight 5, pierce resisted while blocking, three levels.
    /// </para>
    /// </summary>
    public static class KrakenShield
    {
        /// <summary>The shield's name word, by which <see cref="ParryBite"/> knows it in any player's hand.</summary>
        public const string Name = "$item_" + Word;

        private const string Word = "ecp_shieldkraken";
        private const string Asset = "ecp_kraken_beak_shield";
        private const string GameShield = "ShieldSilver";

        /// <summary>The grip on the shield's back, in the model's space (front +Z, up +Y, origin under it).</summary>
        private static readonly Vector3 Grip = new Vector3(0f, 0.565f, -0.16f);

        public static GameObject Build(ZNetScene scene, AssetBundle bundle)
        {
            GameObject shield = LootItem.Copy(scene, GameShield, KrakenLoot.Shield);
            Transform? game = LootItem.GameModel(shield);
            Quaternion turn = game != null ? game.localRotation : Quaternion.Euler(90f, 0f, 0f);
            Transform model = LootItem.Swap(shield, EmbeddedBundle.Prefab(bundle, Asset), 1f, 0.25f);
            model.localRotation = turn;
            model.localPosition = -(turn * Grip);
            LootItem.Describe(shield, bundle, Asset, Word);
            Stats(shield.GetComponent<ItemDrop>().m_itemData.m_shared);
            return shield;
        }

        private static void Stats(ItemDrop.ItemData.SharedData shared)
        {
            shared.m_blockPower = 70f;
            shared.m_blockPowerPerLevel = 6f;
            shared.m_deflectionForce = 110f;
            shared.m_deflectionForcePerLevel = 5f;
            shared.m_timedBlockBonus = 1.5f;
            shared.m_perfectBlockAdrenaline = 5f;
            shared.m_movementModifier = -0.05f;
            shared.m_maxDurability = 300f;
            shared.m_durabilityPerLevel = 50f;
            shared.m_weight = 4f;
            shared.m_maxQuality = 3;
            shared.m_variants = 0;
            shared.m_damageModifiers = new List<HitData.DamageModPair>
            {
                new HitData.DamageModPair { m_type = HitData.DamageType.Pierce, m_modifier = HitData.DamageModifier.Resistant },
            };
        }
    }
}

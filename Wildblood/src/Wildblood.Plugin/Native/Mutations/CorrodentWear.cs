// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Corrodent: a player's armour wears `durability` times as fast under its hits. The game wears one worn armour piece
    /// by what a hit dealt through the armour, on the hit player's own machine, where their gear lives
    /// (<c>CorrodentPatch</c>): before it does, the durability of every worn piece is noted, and after, whatever was lost
    /// is lost `durability` times over, never below 0. Only its own hits count - its melee, its thrown and shot
    /// projectiles, its area attacks - told apart by the attacker the hit carries, which the hit player's machine has
    /// loaded and reads the traits of from the creature's ZDO. Shields are not armour and wear as usual. The player sees
    /// it: at most every few seconds a line names the piece that corroded and how much of it is left. Event-shaped: no
    /// component.
    /// </summary>
    public static class CorrodentWear
    {
        /// <summary>Seconds between two lines telling the player their armour corroded.</summary>
        private const float TellEvery = 8f;

        private static float _toldAt = float.NegativeInfinity;

        /// <summary>What a hit by a Corrodent creature finds on the player before the game wears their armour.</summary>
        public struct Before
        {
            public bool Active;
            public float Factor;
            public float Chest;
            public float Legs;
            public float Helmet;
            public float Shoulder;
        }

        /// <summary>The worn pieces' durability before the hit, when a Corrodent creature dealt it; inactive otherwise.</summary>
        public static Before Capture(Player player, HitData hit)
        {
            float factor = Factor(hit.GetAttacker());
            if (factor == 1f)
            {
                return default;
            }
            return new Before
            {
                Active = true, Factor = factor,
                Chest = Durability(player.m_chestItem), Legs = Durability(player.m_legItem),
                Helmet = Durability(player.m_helmetItem), Shoulder = Durability(player.m_shoulderItem),
            };
        }

        /// <summary>Whatever the game took from a piece is taken `durability` times over; the player is told.</summary>
        public static void Apply(Player player, Before before)
        {
            ItemDrop.ItemData? worn = null;
            worn = Wear(player.m_chestItem, before.Chest, before.Factor) ?? worn;
            worn = Wear(player.m_legItem, before.Legs, before.Factor) ?? worn;
            worn = Wear(player.m_helmetItem, before.Helmet, before.Factor) ?? worn;
            worn = Wear(player.m_shoulderItem, before.Shoulder, before.Factor) ?? worn;
            if (worn != null)
            {
                Tell(player, worn);
            }
        }

        // The wear is its gain, so a large star enhances it, as a `1 + bonus` multiplier; never negative.
        private static float Factor(Character? attacker)
        {
            if (attacker == null || attacker.IsPlayer())
            {
                return 1f;
            }
            EliteController controller = attacker.GetComponent<EliteController>();
            if (controller == null || !controller.Ready || !controller.Traits.Has(Mutation.Corrodent))
            {
                return 1f;
            }
            return Mathf.Max(0f, Enhance.Stat(controller.Rules, controller.Traits, Mutation.Corrodent, Fields.Durability));
        }

        private static float Durability(ItemDrop.ItemData? item) => item != null ? item.m_durability : 0f;

        private static ItemDrop.ItemData? Wear(ItemDrop.ItemData? item, float before, float factor)
        {
            if (item == null || item.m_durability >= before)
            {
                return null;
            }
            item.m_durability = Mathf.Max(0f, before - (before - item.m_durability) * factor);
            return item;
        }

        /// <summary>A line with the piece's icon, its name and what is left of it; none for a piece that never wears.</summary>
        private static void Tell(Player player, ItemDrop.ItemData item)
        {
            if (Time.time - _toldAt < TellEvery || !item.m_shared.m_useDurability)
            {
                return;
            }
            _toldAt = Time.time;
            string name = global::Localization.instance != null ? global::Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;
            int left = Mathf.FloorToInt(item.GetDurabilityPercentage() * 100f);
            player.Message(MessageHud.MessageType.TopLeft, ExtraPotions.Wildblood.Runtime.WildbloodText.Format("corrodent_wear", name, left), 0, item.GetIcon());
        }
    }
}

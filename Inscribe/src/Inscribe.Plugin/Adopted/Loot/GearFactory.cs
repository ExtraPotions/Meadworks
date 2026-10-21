// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rolling;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Sockets;

namespace ExtraPotions.Inscribe.Adopted.Loot
{
    /// <summary>
    /// Builds a pre-rolled gear item (drops.md section 8): a clone of the base prefab's item data with its drop prefab,
    /// upgrade level 1, full durability, the world's world level, a random style variant, no crafter, then a fresh roll
    /// at the drawn rarity through <see cref="ItemRoller.RollFresh"/> under the base's own class and level, its sockets
    /// (<see cref="SocketDrops"/>), written with
    /// <see cref="ItemState.Write"/> (reason <c>drop</c>), then the API's <see cref="LootGenerated"/> listeners. If the
    /// pool cannot fill the rarity, the next lower rarity that may drop is tried.
    /// Runs on the peer that spawns the drop (the creature's owner). Nothing here touches the world.
    /// </summary>
    public static class GearFactory
    {
        private static bool _rollerFailureLogged;

        /// <summary>The rolled item, or null when no magic rarity can be rolled on this base (nothing then drops).</summary>
        public static ItemDrop.ItemData? Build(GearBase gear, RarityDef rarity, bool cheated, Random random, RuleSet rules)
        {
            ItemDrop.ItemData item = NewItem(gear, cheated, random);
            RarityDef? rolled = RollInto(item, gear, rarity, random, rules, out bool rollerBroken);
            if (rolled == null && !rollerBroken)
            {
                return null;
            }
            item.m_durability = item.GetMaxDurability();   // after the write, so durability affixes count
            if (rolled != null)
            {
                LootGenerated.Raise(item);
            }
            return item;
        }

        /// <summary>A plain copy of the base, ready for state: nothing of the prefab's instance data carries over.</summary>
        public static ItemDrop.ItemData NewItem(GearBase gear, bool cheated, Random random)
        {
            ItemDrop.ItemData item = gear.Template.m_itemData.Clone();
            item.m_dropPrefab = gear.Prefab;
            item.m_stack = 1;
            item.m_quality = 1;
            item.m_worldLevel = Game.m_worldLevel;
            item.m_cheated = cheated;
            item.m_crafterID = 0L;
            item.m_crafterName = "";
            item.m_pickedUp = false;
            item.m_equipped = false;
            item.m_customData.Clear();
            int variants = item.m_shared.m_variants;
            item.m_variant = variants > 1 ? random.Next(variants) : 0;   // judgement call: any style of the base
            return item;
        }

        // Walks down from the drawn rarity. Out: true when the roller itself failed (not yet implemented or threw):
        // the item then drops plain, logged once, rather than losing the drop.
        private static RarityDef? RollInto(ItemDrop.ItemData item, GearBase gear, RarityDef drawn, Random random, RuleSet rules,
            out bool rollerBroken)
        {
            rollerBroken = false;
            for (RarityDef? rarity = drawn; rarity != null && !rarity.IsBase; rarity = rules.Economy.Previous(rarity))
            {
                if (!Worth(rarity, drawn, gear))
                {
                    continue;
                }
                if (!TryRoll(gear, rarity, random, rules, out RollOutcome outcome))
                {
                    rollerBroken = true;
                    return null;
                }
                if (outcome.Success || outcome.Failure != RollFailure.NoEligibleAffix)
                {
                    return outcome.Success ? Commit(item, SocketDrops.Add(outcome.State!, gear.Class, rarity, random), rarity) : null;
                }
            }
            return null;
        }

        // A lower fallback rarity must be able to drop at all, and no rarity is tried that the pool cannot fill.
        private static bool Worth(RarityDef rarity, RarityDef drawn, GearBase gear) =>
            (rarity == drawn || rarity.DropWeight > 0f) && rarity.MinAffixes <= gear.CapacityFor(rarity);

        private static bool TryRoll(GearBase gear, RarityDef rarity, Random random, RuleSet rules, out RollOutcome outcome)
        {
            RollContext context = new RollContext { Class = gear.Class, Level = gear.Level, Random = random, Rules = rules };
            try
            {
                outcome = ItemRoller.RollFresh(ItemState.Empty, rarity, context);
                return true;
            }
            catch (Exception e)
            {
                outcome = RollOutcome.Fail(RollFailure.None);
                if (!_rollerFailureLogged)
                {
                    _rollerFailureLogged = true;
                    Log.Error($"gear drops: the inscription roller failed, magic drops fall back to plain items: {e.Message}");
                }
                return false;
            }
        }

        private static RarityDef? Commit(ItemDrop.ItemData item, ItemState state, RarityDef rarity)
        {
            bool written;
            using (ItemChanges.Because(ItemChanges.Drop))
            {
                written = ItemState.Write(item, state);
            }
            if (written)
            {
                return rarity;
            }
            Log.Warn($"gear drops: state for {item.m_dropPrefab?.name} was refused; not dropped");
            return null;
        }
    }
}

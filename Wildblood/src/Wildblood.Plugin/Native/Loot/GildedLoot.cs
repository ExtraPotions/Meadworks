// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Loot
{
    /// <summary>
    /// Gilded's pay-out, the reason players chase it: its drops multiplied by <c>loot</c> and a purse of <c>bonus
    /// item</c> on top. Gilded is the one mutation that pays (`loot.md` keeps the others from paying) because it never
    /// fights back - the chase is the fight. The multiplier touches exactly the rows the global multiplier touches: the
    /// creature's own table and rows the rule file names, trophies only when the trophy switch says so, another mod's
    /// rows never. In every mode but Vanilla it joins the engine's final multiply, so one rounding covers both; in
    /// Vanilla, where the engine stands aside, it scales the creature's own rows alone, the way a boss aspect pays there.
    /// The purse comes after every multiplier and is never scaled by one: <c>bonus amount</c> x (1 + stars), held to
    /// the game's per-row cap. Values come from the rules active at death, so a rule edit re-tunes a creature already
    /// walking about - the same hot reload every other loot setting gets. A tamed Gilded creature pays like any other:
    /// it never ran, so there was no chase to reward, and Gilded passes to offspring - a paying tame one would turn a
    /// breeding pen into an endless purse.
    /// </summary>
    internal static class GildedLoot
    {
        private static readonly HashSet<string> _warnedMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The loot multiplier: 1 unless it pays, else <c>loot</c> read as a stat, so a large star raises it.</summary>
        public static float Factor(EliteController controller)
        {
            if (!Pays(controller))
            {
                return 1f;
            }
            return Mathf.Max(0f, Enhance.Stat(LiveRules(controller), controller.Traits, Mutation.Gilded, Fields.Loot));
        }

        /// <summary>Vanilla mode's whole treatment: the creature's own rows scaled, then the purse.</summary>
        public static void ApplyAlone(CharacterDrop drop, EliteController controller, List<KeyValuePair<GameObject, int>> result)
        {
            ApplyMultiplier(drop, controller, result);
            AddBonus(controller, result);
        }

        internal static void ApplyMultiplier(CharacterDrop drop, EliteController controller, List<KeyValuePair<GameObject, int>> result)
        {
            if (!Pays(controller))
            {
                return;
            }
            DropRoller.ScaleOwnRows(drop, result, Factor(controller), RuleState.Active.Loot.MultiplyTrophies);
        }

        /// <summary>The purse: <c>bonus amount</c> (a raw magnitude, so a large star raises it) per star plus one.</summary>
        public static void AddBonus(EliteController controller, List<KeyValuePair<GameObject, int>> result)
        {
            if (!Pays(controller))
            {
                return;
            }
            BiomeRules rules = LiveRules(controller);
            float each = Enhance.Magnitude(rules, controller.Traits, Mutation.Gilded, Fields.BonusAmount);
            int amount = Mathf.Min(Mathf.RoundToInt(each * (1 + controller.Traits.Stars)), DropRoller.AmountCap);
            GameObject? item = amount > 0 ? FindItem(rules.PrefabOf(Mutation.Gilded, Fields.BonusItem)) : null;
            if (item != null)
            {
                DropRoller.Add(result, item, amount);
            }
        }

        /// <summary>A wild Gilded creature pays; a tamed one, which never ran from anyone, does not.</summary>
        private static bool Pays(EliteController controller) =>
            controller.Traits.Has(Mutation.Gilded) && (controller.Creature == null || !controller.Creature.IsTamed());

        /// <summary>The item <c>bonus item</c> names, by prefab name in the item database; warned once per bad name.</summary>
        private static GameObject? FindItem(string name)
        {
            GameObject? item = ObjectDB.instance != null && !string.IsNullOrEmpty(name)
                ? ObjectDB.instance.GetItemPrefab(name) : null;
            if (item == null && _warnedMissing.Add(name))
            {
                Log.Warn($"Gilded bonus item '{name}' is not an item this game knows - the bonus is skipped");
            }
            return item;
        }

        /// <summary>The creature's rules as they stand now - its biome's, with its own <c>creatures:</c> mutation power
        /// merged over them by prefab - and its resolve-time copy only when the ZDO is gone.</summary>
        private static BiomeRules LiveRules(EliteController controller)
        {
            ZDO? zdo = controller.View != null && controller.View.IsValid() ? controller.View.GetZDO() : null;
            return zdo != null
                ? RuleState.Active.For(TraitStore.GetBiome(zdo), Utils.GetPrefabName(controller.gameObject))
                : controller.Rules;
        }
    }
}

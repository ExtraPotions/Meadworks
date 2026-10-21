// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe classes</c> (classes-and-tiers.md section 9): every item class in classification order with its group,
    /// whether it rolls, its damage scale and drop weight, how many items of the object database it holds and their item
    /// level range, and its pool size (inscriptions that list it as best and as allowed). Reads this machine's object
    /// database and the running rules; command path only.
    /// </summary>
    internal static class ClassesCommand
    {
        public const string Grammar = "inscribe classes";

        private sealed class Tally
        {
            public int Items;
            public int MinLevel = int.MaxValue;
            public int MaxLevel;
        }

        public static void Run(CommandCall call)
        {
            Dictionary<string, Tally> tallies = Count(ObjectDB.instance);
            IReadOnlyList<ItemClass> classes = ItemClasses.All;
            call.Reply($"{classes.Count} item classes, in classification order" + (ObjectDB.instance == null ? " (no object database yet: no item counts)" : ""));
            foreach (ItemClass itemClass in classes)
            {
                tallies.TryGetValue(itemClass.Id, out Tally? tally);
                call.Detail(Line(itemClass, tally, ActiveRules.Current.Affixes));
            }
        }

        private static Dictionary<string, Tally> Count(ObjectDB? db)
        {
            Dictionary<string, Tally> tallies = new Dictionary<string, Tally>(System.StringComparer.Ordinal);
            Recipes.Refresh();
            foreach (GameObject go in db?.m_items ?? new List<GameObject>())
            {
                ItemDrop? drop = go != null ? go.GetComponent<ItemDrop>() : null;
                string? id = drop != null ? ItemClasses.Classify(drop.m_itemData).ClassId : null;
                if (id == null)
                {
                    continue;
                }
                if (!tallies.TryGetValue(id, out Tally tally))
                {
                    tally = new Tally();
                    tallies[id] = tally;
                }
                int level = ItemTier.Of(go!.name);
                tally.Items++;
                tally.MinLevel = System.Math.Min(tally.MinLevel, level);
                tally.MaxLevel = System.Math.Max(tally.MaxLevel, level);
            }
            return tallies;
        }

        private static string Line(ItemClass itemClass, Tally? tally, AffixRules affixes)
        {
            int best = 0, allowed = 0;
            foreach (PoolEntry entry in affixes.Pool(itemClass.Id))
            {
                best += entry.Fit == ClassFit.Best ? 1 : 0;
                allowed += entry.Fit == ClassFit.Allowed ? 1 : 0;
            }
            string items = tally == null ? "0 items" : $"{tally.Items} items, levels {tally.MinLevel}-{tally.MaxLevel}";
            return $"{itemClass.Id} ({itemClass.Group}){(itemClass.Rolls ? "" : " never magic")}: {items}; "
                + $"pool {best} best + {allowed} allowed; damage_scale {Numbers.Format(itemClass.DamageScale)}, "
                + $"drop_weight {Numbers.Format(itemClass.DropWeight)}";
        }
    }
}

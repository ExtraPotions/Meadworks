// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe tiers</c> (item-tier.md section 7, DECISIONS.md TIR-5): writes
    /// <c>ExtraPotions.Inscribe.Adopted_item_tiers_reference.yml</c> to the config folder: every magic base with its item class, item level
    /// (1-8) and the step that decided it, then the craft materials of magic-base recipes that are not in <c>item_tiers.materials</c>
    /// (items with an explicit override skipped), then the map keys that match no prefab. Not read by the mod (it is
    /// not in either family's pattern). Sorted by name so two runs diff cleanly. Runs on the caller's machine.
    /// </summary>
    internal static class TiersCommand
    {
        public const string FileName = "ExtraPotions.Inscribe.Adopted_item_tiers_reference.yml";

        public static void Run(CommandCall call)
        {
            ObjectDB? db = ObjectDB.instance;
            if (db == null || db.m_items == null)
            {
                call.Reply("the object database is not loaded yet.");
                return;
            }
            Recipes.Refresh();
            ItemTierMaps maps = ActiveRules.Current.Economy.ItemTiers;
            SortedDictionary<string, ItemDrop.ItemData> bases = MagicBases(db);
            SortedDictionary<string, SortedSet<string>> unmapped = UnmappedMaterials(bases, maps);
            StringBuilder sb = new StringBuilder();
            sb.Append("# ExtraPotions.Inscribe.Adopted item tier reference, written by 'inscribe tiers' (item-tier.md section 7). Not read by the mod.\n");
            sb.Append($"# {bases.Count} magic bases, {unmapped.Count} unmapped materials.\n");
            WriteItems(sb, bases);
            WriteUnmapped(sb, unmapped);
            TierMapKeys.WriteUnknown(sb, db, maps);
            string path = ConfigOutput.Write(FileName, sb.ToString());
            call.Reply($"wrote {bases.Count} magic bases and {unmapped.Count} unmapped materials to {path}");
        }

        private static SortedDictionary<string, ItemDrop.ItemData> MagicBases(ObjectDB db)
        {
            SortedDictionary<string, ItemDrop.ItemData> bases = new SortedDictionary<string, ItemDrop.ItemData>(StringComparer.Ordinal);
            foreach (GameObject go in db.m_items)
            {
                ItemDrop? drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop != null && ItemClasses.IsMagicBase(drop.m_itemData))
                {
                    bases[go!.name] = drop.m_itemData;
                }
            }
            return bases;
        }

        private static void WriteItems(StringBuilder sb, SortedDictionary<string, ItemDrop.ItemData> bases)
        {
            sb.Append("\nitems:\n");
            foreach (KeyValuePair<string, ItemDrop.ItemData> pair in bases)
            {
                TierResult level = ItemTier.Explain(pair.Key);
                string itemClass = ItemClasses.Classify(pair.Value).ClassId ?? "-";
                sb.Append($"  {Yaml.Quote(pair.Key)}: {{class: {itemClass}, level: {level.Tier}, source: {Yaml.Quote(level.Source)}}}\n");
            }
        }

        // Craft materials (amount above 0) of magic-base recipes that the material map does not name.
        private static SortedDictionary<string, SortedSet<string>> UnmappedMaterials(SortedDictionary<string, ItemDrop.ItemData> bases, ItemTierMaps maps)
        {
            SortedDictionary<string, SortedSet<string>> unmapped = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (string prefab in bases.Keys)
            {
                if (maps.Items.ContainsKey(prefab))
                {
                    continue;
                }
                foreach (Recipe recipe in Recipes.For(prefab))
                {
                    CollectRecipe(recipe, prefab, maps, unmapped);
                }
            }
            return unmapped;
        }

        private static void CollectRecipe(Recipe recipe, string prefab, ItemTierMaps maps, SortedDictionary<string, SortedSet<string>> unmapped)
        {
            foreach (Piece.Requirement req in recipe.m_resources ?? Array.Empty<Piece.Requirement>())
            {
                string? name = req?.m_resItem != null && req.m_amount > 0 ? req.m_resItem.gameObject.name : null;
                if (name == null || maps.Materials.ContainsKey(name))
                {
                    continue;
                }
                if (!unmapped.TryGetValue(name, out SortedSet<string> users))
                {
                    users = new SortedSet<string>(StringComparer.Ordinal);
                    unmapped[name] = users;
                }
                users.Add(prefab);
            }
        }

        private static void WriteUnmapped(StringBuilder sb, SortedDictionary<string, SortedSet<string>> unmapped)
        {
            sb.Append("\n# Materials used by magic-base recipes that are not in item_tiers.materials. 'crafted: true' ones\n");
            sb.Append("# take the tier of their own recipe (material_depth deep); the others decide nothing.\n");
            sb.Append("unmapped_materials:");
            sb.Append(unmapped.Count == 0 ? " []\n" : "\n");
            foreach (KeyValuePair<string, SortedSet<string>> pair in unmapped)
            {
                List<string> users = new List<string>();
                foreach (string user in pair.Value)
                {
                    users.Add(Yaml.Quote(user));
                }
                sb.Append($"  - {{material: {Yaml.Quote(pair.Key)}, crafted: {(RecipeIndex.HasRecipe(pair.Key) ? "true" : "false")}, "
                    + $"used_by: [{string.Join(", ", users)}]}}\n");
            }
        }
    }

    /// <summary>The <c>unknown_map_keys</c> section: <c>item_tiers</c> keys that match no prefab (a warning, never an error).</summary>
    internal static class TierMapKeys
    {
        public static void WriteUnknown(StringBuilder sb, ObjectDB db, ItemTierMaps maps)
        {
            HashSet<string> stations = StationNames(db);
            sb.Append("\n# item_tiers keys that match no prefab here (renamed by a game update, or a mod that is not installed).\n");
            sb.Append("unknown_map_keys:\n");
            sb.Append("  items: ").Append(List(maps.Items.Keys, name => db.GetItemPrefab(name) != null)).Append('\n');
            sb.Append("  materials: ").Append(List(maps.Materials.Keys, name => db.GetItemPrefab(name) != null)).Append('\n');
            sb.Append("  stations: ").Append(List(maps.Stations.Keys, stations.Contains)).Append('\n');
            sb.Append("  station_levels: ").Append(List(maps.StationLevels.Keys, stations.Contains)).Append('\n');
        }

        // Every crafting and repair station a recipe names, plus the scene's prefabs when a world is loaded.
        private static HashSet<string> StationNames(ObjectDB db)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe != null && recipe.m_craftingStation != null) names.Add(recipe.m_craftingStation.gameObject.name);
                if (recipe != null && recipe.m_repairStation != null) names.Add(recipe.m_repairStation.gameObject.name);
            }
            foreach (GameObject prefab in ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : new List<GameObject>())
            {
                if (prefab != null && prefab.GetComponent<CraftingStation>() != null) names.Add(prefab.name);
            }
            return names;
        }

        private static string List(IEnumerable<string> keys, Func<string, bool> known)
        {
            List<string> unknown = new List<string>();
            foreach (string key in keys)
            {
                if (!known(key))
                {
                    unknown.Add(Yaml.Quote(key));
                }
            }
            unknown.Sort(StringComparer.Ordinal);
            return "[" + string.Join(", ", unknown) + "]";
        }
    }

    /// <summary>Minimal YAML scalar quoting for the hand-written reference files.</summary>
    internal static class Yaml
    {
        /// <summary>A double-quoted scalar with backslash and quote escaped.</summary>
        public static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}

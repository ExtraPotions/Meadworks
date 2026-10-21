// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Woodcutting/Finds/FindReader.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using YamlConfig;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Reads one table of Saga.Finds.yml (the value under a biome or a tree): a list of finds, each a map
    /// of <c>name</c>, <c>weight</c> and <c>items</c>, each item a map of <c>prefab</c>, <c>min</c> and <c>max</c>.
    /// Mistakes that make a find meaningless (no items, an item without a prefab, a negative number, max below min)
    /// are errors, so the file is rejected and the previous one stays; a missing name or an oversized amount is a
    /// warning and the file still applies. Item names are not checked here: the item database may not exist yet.
    /// </summary>
    public static class FindReader
    {
        /// <summary>The largest amount one item of a find can drop; larger values are clamped with a warning.</summary>
        public const int MaxAmount = 999;

        /// <summary>The finds of one table; empty for a key without a value ("Swamp:" turns finds off there).</summary>
        public static List<FindEntry> ReadTable(YamlNode node)
        {
            List<FindEntry> finds = new List<FindEntry>();
            if (node.Kind == YamlNodeKind.Null)
                return finds;
            foreach (YamlNode item in node.Items)
            {
                FindEntry find = ReadFind(item);
                if (find != null)
                    finds.Add(find);
            }
            return finds;
        }

        private static FindEntry ReadFind(YamlNode node)
        {
            if (node.Kind != YamlNodeKind.Map)
            {
                node.Error("a find is a map of name, weight and items");
                return null;
            }
            // Every key is asked for before anything is rejected, so a rejected find reports no unknown keys.
            YamlNode name = node.Get("name");
            float weight = ReadWeight(node.Get("weight"));
            YamlNode itemsNode = node.Get("items");
            List<FindItem> items = ReadItems(itemsNode);
            if (items.Count > 0)
                return new FindEntry(ReadName(name), weight, items);
            // Items that failed to read have reported already; only an empty or missing list is said here.
            if (itemsNode.Count == 0)
                node.Error("a find needs at least one item under 'items'");
            return null;
        }

        private static string ReadName(YamlNode node)
        {
            if (node.TryString(out string name) && name.Trim().Length > 0)
                return name.Trim();
            if (node.Kind == YamlNodeKind.Missing)
                node.Warn("no name; the callout says 'Found something!'");
            return "something";
        }

        /// <summary>1 when not given; a negative weight is an error, 0 keeps the find but never picks it.</summary>
        private static float ReadWeight(YamlNode node)
        {
            if (!node.TryFloat(out float weight))
                return 1f;
            if (weight >= 0f)
                return weight;
            node.Error("the weight cannot be negative");
            return 0f;
        }

        private static List<FindItem> ReadItems(YamlNode node)
        {
            List<FindItem> items = new List<FindItem>();
            if (node.Kind == YamlNodeKind.Missing || node.Kind == YamlNodeKind.Null)
                return items;
            foreach (YamlNode item in node.Items)
            {
                FindItem read = ReadItem(item);
                if (read != null)
                    items.Add(read);
            }
            return items;
        }

        private static FindItem ReadItem(YamlNode node)
        {
            if (node.Kind != YamlNodeKind.Map)
            {
                node.Error("an item is a map like { prefab: Feathers, min: 2, max: 4 }");
                return null;
            }
            YamlNode prefabNode = node.Get("prefab");
            int min = ReadAmount(node.Get("min"), 1);
            int max = ReadAmount(node.Get("max"), min);
            if (!prefabNode.TryString(out string prefab) || prefab.Trim().Length == 0)
            {
                node.Error("an item needs a prefab name, for example 'prefab: Feathers'");
                return null;
            }
            if (min < 0 || max < min)
            {
                node.Error($"min ({min}) must be 0 or more and max ({max}) at least min");
                return null;
            }
            return new FindItem(prefab.Trim(), min, max);
        }

        /// <summary>The amount written, or the fallback when the key is not there; clamped to <see cref="MaxAmount"/>.</summary>
        private static int ReadAmount(YamlNode node, int fallback)
        {
            if (!node.TryInt(out int amount))
                return fallback;
            if (amount <= MaxAmount)
                return amount;
            node.Warn($"{amount} is more than {MaxAmount}; clamped");
            return MaxAmount;
        }
    }
}

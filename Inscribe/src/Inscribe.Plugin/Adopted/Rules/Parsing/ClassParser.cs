// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Items;
using YamlDotNet.RepresentationModel;
using ItemType = ItemDrop.ItemData.ItemType;
using SkillType = Skills.SkillType;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads <c>classes:</c> (classes-and-tiers.md section 1), the item classes in classification order. Unknown item
    /// type and skill names in a match rule are errors: a rule that silently lost its <c>types</c> would match far
    /// more than it says. A prefab listed in two classes' <c>items</c> is a warning (the first class keeps it).
    /// </summary>
    internal static class ClassParser
    {
        private static readonly string[] MatchKeys = { "types", "skills", "two_handed", "stackable", "prefixes", "name_contains" };

        public static List<ItemClass> Parse(MapReader root)
        {
            List<ItemClass> classes = new List<ItemClass>();
            YamlSequenceNode? seq = root.Seq("classes");
            foreach (YamlNode node in seq?.Children ?? new List<YamlNode>())
            {
                ItemClass? found = ParseOne(node, root.Issues);
                if (found != null)
                {
                    classes.Add(found);
                }
            }
            WarnSharedItems(root, classes);
            return classes;
        }

        /// <summary>One class entry (also the shape a registered class's JSON takes, api.md section 2).</summary>
        public static ItemClass? ParseOne(YamlNode node, RuleIssues issues)
        {
            string? id = node is YamlMappingNode m ? new MapReader(m, "classes[?]", issues).Id("id") : null;
            if (id == null)
            {
                issues.Error("classes[?]", node, "a class entry needs a valid 'id'");
                return null;
            }
            MapReader r = new MapReader((YamlMappingNode)node, $"classes[{id}]", issues);
            r.Unknown(ItemClass.Keys);
            ItemClass c = ItemClass.Create(id);
            c.Group = r.Id("group") ?? c.Group;
            c.Name = r.Str("name") ?? c.Name;
            c.Rolls = r.Bool("rolls", true);
            c.DamageScale = r.Float("damage_scale", 1f, 0.01f);
            c.DropWeight = r.Float("drop_weight", 1f, 0f);
            c.Match = ReadMatch(r);
            c.Items = r.Strings("items") ?? new List<string>();
            c.Named = Named(r.Map);
            return c;
        }

        private static List<ClassMatch> ReadMatch(MapReader r)
        {
            List<ClassMatch> rules = new List<ClassMatch>();
            YamlSequenceNode? seq = r.Seq("match");
            for (int i = 0; seq != null && i < seq.Children.Count; i++)
            {
                if (seq.Children[i] is YamlMappingNode map)
                {
                    rules.Add(ReadRule(new MapReader(map, $"{r.At("match")}[{i}]", r.Issues)));
                }
                else
                {
                    r.Issues.Error(r.At("match"), seq.Children[i], "a match rule should look like { types: [Helmet] }");
                }
            }
            return rules;
        }

        private static ClassMatch ReadRule(MapReader r)
        {
            r.Unknown(MatchKeys);
            return new ClassMatch
            {
                Types = Names<ItemType>(r, "types", "item type"),
                Skills = Names<SkillType>(r, "skills", "skill"),
                TwoHanded = r.Has("two_handed") ? r.Bool("two_handed", false) : (bool?)null,
                Stackable = r.Has("stackable") ? r.Bool("stackable", false) : (bool?)null,
                Prefixes = r.Strings("prefixes"),
                NameContains = r.Strings("name_contains"),
            };
        }

        // The game's own enum names (Helmet, TwoHandedWeapon, Swords, ElementalMagic), case-insensitive.
        private static List<T>? Names<T>(MapReader r, string key, string what) where T : struct, Enum
        {
            List<string>? names = r.Strings(key);
            if (names == null)
            {
                return null;
            }
            List<T> values = new List<T>(names.Count);
            foreach (string name in names)
            {
                if (name.Length > 0 && char.IsLetter(name[0]) && Enum.TryParse(name, true, out T value) && Enum.IsDefined(typeof(T), value))
                {
                    values.Add(value);
                }
                else
                {
                    r.Error(key, $"'{name}' is not a game {what} name");
                }
            }
            return values;
        }

        private static HashSet<string> Named(YamlMappingNode map)
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, YamlNode> pair in YamlLists.Pairs(map))
            {
                keys.Add(pair.Key);
            }
            return keys;
        }

        private static void WarnSharedItems(MapReader root, List<ItemClass> classes)
        {
            Dictionary<string, string> owner = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ItemClass c in classes)
            {
                foreach (string prefab in c.Items)
                {
                    if (owner.TryGetValue(prefab, out string first) && first != c.Id)
                    {
                        root.Warn("classes", $"{prefab} is in the items of '{first}' and '{c.Id}'; '{first}' keeps it");
                    }
                    else
                    {
                        owner[prefab] = c.Id;
                    }
                }
            }
        }
    }
}

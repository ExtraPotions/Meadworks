// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// Item classes and item levels through the API (api.md section 2), over the internal registry in <c>Items/</c>:
    /// a class from JSON is read by the YAML's own class parser (<see cref="ClassParser.ParseOne"/>) and registered
    /// under the YAML; claims and classifiers go straight to <see cref="ItemClasses"/>; a level goes to
    /// <see cref="CodeLevels"/>. Each change schedules one rules refresh at the end of the frame, so every cache built
    /// from classes and levels (gear pool, tooltips, the unknown-class check) catches up.
    /// </summary>
    internal static class ApiClasses
    {
        public static bool Register(string? json)
        {
            const string endpoint = "RegisterItemClass";
            RuleIssues issues = new RuleIssues();
            YamlMappingNode? map = ApiJson.Map(json, endpoint, issues);
            if (map == null)
            {
                return false;
            }
            ItemClass? def = ClassParser.ParseOne(map, issues);
            if (!ApiJson.Accept(issues, endpoint) || def == null)
            {
                return false;
            }
            def.Named = new HashSet<string>(StringComparer.Ordinal);   // a registered class names nothing: the YAML lies over it
            return Refreshed(ItemClasses.RegisterClass(def));
        }

        public static bool Claim(string? classId, string[]? prefabs)
        {
            if (classId == null || prefabs == null)
            {
                return false;
            }
            List<string> names = new List<string>(prefabs.Length);
            foreach (string prefab in prefabs)
            {
                if (!string.IsNullOrEmpty(prefab))
                {
                    names.Add(prefab);
                }
            }
            return Refreshed(ItemClasses.ClaimItems(classId, names));
        }

        public static bool AddClassifier(string? id, Func<ItemDrop.ItemData, string>? classify) =>
            id != null && classify != null && Refreshed(ItemClasses.AddClassifier(id, classify!));

        public static bool RemoveClassifier(string? id) => id != null && Refreshed(ItemClasses.RemoveClassifier(id));

        public static bool SetLevel(string? prefab, int level)
        {
            if (string.IsNullOrEmpty(prefab) || level < 1 || level > TierResult.MaxLevel)
            {
                Log.Warn($"API SetItemLevel: '{prefab}' {level} refused (a prefab name and a level from 1 to {TierResult.MaxLevel})");
                return false;
            }
            CodeLevels.Set(prefab!, level);
            return Refreshed(true);
        }

        public static string? ClassOf(ItemDrop.ItemData? item) => ItemClasses.ClassOf(item)?.Id;

        public static int LevelOf(ItemDrop.ItemData? item) => item == null ? 0 : ItemTier.Of(item);

        private static bool Refreshed(bool changed)
        {
            if (changed)
            {
                RuleRebuild.Request(inscriptions: false);
            }
            return changed;
        }
    }
}

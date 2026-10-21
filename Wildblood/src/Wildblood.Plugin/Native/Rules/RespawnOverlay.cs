// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>Reads the file's top-level <c>respawning</c> block onto the defaults; anything omitted keeps its own.</summary>
    internal static class RespawnOverlay
    {
        public static void Apply(RespawnRules rules, YamlMappingNode block, List<string> errors)
        {
            rules.Camps = YamlRead.Bool(block, "camps", rules.Camps, errors);
            rules.Dungeons = YamlRead.Bool(block, "dungeons", rules.Dungeons, errors);
            rules.DungeonLoot = YamlRead.Bool(block, "dungeon loot", rules.DungeonLoot, errors);
            rules.CampDays = Days(block, "camp days", rules.CampDays, errors);
            rules.DungeonDays = Days(block, "dungeon days", rules.DungeonDays, errors);
            rules.DungeonLootDays = Days(block, "dungeon loot days", rules.DungeonLootDays, errors);
        }

        private static float Days(YamlMappingNode block, string key, float current, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, key);
            if (node == null)
            {
                return current;
            }
            if (YamlRead.TryFloat(node, out float value) && value >= 0f)
            {
                return value;
            }
            YamlRead.AddError(errors, node, $"'{key}' is not a number of days (0 or more)");
            return current;
        }
    }
}

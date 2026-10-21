// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Reads the file's top-level <c>bosses</c> block onto the boss defaults: the stars switch, the star distribution,
    /// the boss star-power lines and the <c>aspects</c> block. Anything the block leaves out keeps its default, the same way a biome block does.
    /// </summary>
    internal static class BossOverlay
    {
        public static void Apply(BossRules boss, YamlMappingNode block, List<string> errors, List<string> warnings)
        {
            boss.Enabled = YamlRead.Bool(block, "stars", boss.Enabled, errors);
            boss.Rewards = YamlRead.Bool(block, "rewards", boss.Rewards, errors);
            boss.HarderEachKill = YamlRead.Bool(block, "harder each kill", boss.HarderEachKill, errors);
            float[]? chances = YamlRead.Floats(YamlRead.Child(block, "star chances"), errors, "boss star chances");
            if (chances != null && chances.Length > 0)
            {
                boss.StarChances = chances;
            }
            ApplyPower(boss.Star, block, errors, warnings);
            if (YamlRead.Child(block, Fields.Aspects) is YamlNode aspects
                && YamlRead.Map(aspects, errors, "'aspects'") is YamlMappingNode map)
            {
                AspectOverlay.Apply(boss.Aspects, map, errors, warnings);
            }
        }

        /// <summary>The boss star lines. A `drops` line, which an older file still carries, only warns: a boss's stars pay
        /// in heads and never multiply its other drops (<c>Loot.StarDrops</c>).</summary>
        private static void ApplyPower(StarPower star, YamlMappingNode block, List<string> errors, List<string> warnings)
        {
            if (!(YamlRead.Child(block, "star power") is YamlMappingNode map))
            {
                return;
            }
            star.Growth = Line(map, Fields.Growth, star.Growth, errors);
            star.Hp = Line(map, Fields.Hp, star.Hp, errors);
            star.Attack = Line(map, Fields.Attack, star.Attack, errors);
            star.SwingSpeed = Line(map, Fields.SwingSpeed, star.SwingSpeed, errors);
            star.Speed = Line(map, Fields.Speed, star.Speed, errors);
            if (YamlRead.Child(map, Fields.Drops) != null)
            {
                warnings.Add($"bosses star power '{Fields.Drops}' is no longer used: a boss's stars pay in heads, and its "
                    + "other drops are never multiplied by stars. Delete the line.");
            }
        }

        private static float[] Line(YamlMappingNode map, string key, float[] current, List<string> errors)
        {
            float[]? values = YamlRead.Floats(YamlRead.Child(map, key), errors, $"bosses star power {key}");
            return values != null && values.Length > 0 ? values : current;
        }
    }
}

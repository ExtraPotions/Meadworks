// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using ExtraPotions.Wildblood.Adopted.Util;

namespace ExtraPotions.Wildblood.Adopted.Commands
{
    /// <summary>
    /// Formats and writes creature_reference.yml: biome blocks first (a creature that spawns in two biomes appears
    /// under both), then every boss, then the unassigned creatures nothing spawns in the open world. The header
    /// tells a reader - human or assistant - exactly what the file is for and how the rules that use it look, so a
    /// single paste of this file plus creature_rules.yml carries its own instructions. This file is a report: it is
    /// overwritten on every run and never read back.
    /// </summary>
    internal static class ReferenceReport
    {
        public const string FileName = "creature_reference.yml";

        // Progression order, so a reader meets the biomes in the order a player does.
        private static readonly Heightmap.Biome[] BiomeOrder =
        {
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Ocean,
            Heightmap.Biome.Mountain, Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands,
            Heightmap.Biome.DeepNorth,
        };

        public static string? Write(List<ReferenceCommand.Entry> entries)
        {
            try
            {
                string path = Path.Combine(Paths.ConfigPath, FileName);
                File.WriteAllText(path, Render(entries));
                return path;
            }
            catch (Exception e)
            {
                Log.Error($"could not write {FileName}: {e.Message}");
                return null;
            }
        }

        private static string Render(List<ReferenceCommand.Entry> entries)
        {
            StringBuilder text = new StringBuilder();
            Header(text);
            foreach (Heightmap.Biome biome in BiomeOrder)
            {
                BiomeBlock(text, biome, entries);
            }
            Section(text, "bosses", entries.Where(e => e.Boss), indent: "  ");
            text.AppendLine().AppendLine("# Not in any open-world spawn list: spawned by camps, dungeons, events or");
            text.AppendLine("# other mods' own logic. Their loot rules work exactly the same.");
            Section(text, "unassigned", entries.Where(e => !e.Boss && e.Biomes == Heightmap.Biome.None), indent: "  ");
            return text.ToString();
        }

        private static void Header(StringBuilder text)
        {
            text.AppendLine("# Elite Creatures Reborn - creature reference, generated from this game install");
            text.AppendLine($"# on {DateTime.Now:yyyy-MM-dd} by `wildblood reference`. Rerun the command after a game");
            text.AppendLine("# update or a mod change; this file is a report, and editing it changes nothing.");
            text.AppendLine("#");
            text.AppendLine("# Every creature the game has registered appears below - modded ones included -");
            text.AppendLine("# under its exact prefab name, with its vanilla drop table: amount [min, max],");
            text.AppendLine("# chance in percent. To control loot, paste this file and creature_rules.yml at");
            text.AppendLine("# an assistant, describe the economy you want, and put the `creatures:` rules it");
            text.AppendLine("# writes back into creature_rules.yml. The rule format is documented there.");
            text.AppendLine();
        }

        private static void BiomeBlock(StringBuilder text, Heightmap.Biome biome, List<ReferenceCommand.Entry> entries)
        {
            List<ReferenceCommand.Entry> here =
                entries.Where(e => !e.Boss && (e.Biomes & biome) != Heightmap.Biome.None).ToList();
            if (here.Count == 0)
            {
                return;
            }
            text.AppendLine($"{biome}:");
            Section(text, "creatures", here, indent: "    ", key: "  ");
        }

        private static void Section(StringBuilder text, string name, IEnumerable<ReferenceCommand.Entry> entries,
            string indent, string key = "")
        {
            List<ReferenceCommand.Entry> rows = entries.OrderBy(e => e.Health).ToList();
            text.AppendLine($"{key}{name}:");
            foreach (ReferenceCommand.Entry entry in rows)
            {
                EntryBlock(text, entry, indent);
            }
            if (rows.Count == 0)
            {
                text.AppendLine($"{indent}[]");
            }
        }

        private static void EntryBlock(StringBuilder text, ReferenceCommand.Entry entry, string indent)
        {
            text.AppendLine($"{indent}- prefab: {entry.Prefab}".PadRight(38) + $"# {entry.Display} - hp {entry.Health:0.#}");
            if (entry.Drops.Count == 0)
            {
                text.AppendLine($"{indent}  drops: []");
                return;
            }
            text.AppendLine($"{indent}  drops:");
            foreach (CharacterDrop.Drop drop in entry.Drops)
            {
                if (drop.m_prefab != null)
                {
                    text.AppendLine($"{indent}    - {Row(drop)}");
                }
            }
        }

        private static string Row(CharacterDrop.Drop drop)
        {
            string chance = (drop.m_chance * 100f).ToString("0.##", CultureInfo.InvariantCulture);
            string row = $"{{ item: {drop.m_prefab.name}, amount: [{drop.m_amountMin}, {drop.m_amountMax}], chance: {chance}";
            return drop.m_onePerPlayer ? row + ", one per player: true }" : row + " }";
        }
    }
}

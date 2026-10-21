// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Records/AnglerLog.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The angler's log and records, in the player's own Player.m_customData, which Player.Save writes into the character
    /// file, so both follow the character between worlds and survive relogs. Only the angler's own client reads or writes
    /// them.
    /// <list type="bullet">
    /// <item><see cref="Keys.FishLog"/>: "Fish1=7,Fish2=1": per fish prefab, a mask of the levels landed (bit n-1 for
    /// level n; a legendary is level 6).</item>
    /// <item><see cref="Keys.FishRecords"/>: "Fish2=9.8:5": per fish prefab, the heaviest catch and its level.</item>
    /// </list>
    /// </summary>
    public static class AnglerLog
    {
        private const char EntrySeparator = ',';
        private const char ValueSeparator = '=';
        private const char LevelSeparator = ':';

        /// <summary>The mask of levels of this fish the character has landed; 0 when none.</summary>
        public static int Levels(Player player, string prefab) =>
            Read(player, Keys.FishLog).TryGetValue(prefab, out string mask) && int.TryParse(mask, NumberStyles.Integer, CultureInfo.InvariantCulture, out int levels) ? levels : 0;

        public static bool HasLevel(Player player, string prefab, int level) => (Levels(player, prefab) & Bit(level)) != 0;

        public static void AddLevel(Player player, string prefab, int level)
        {
            Dictionary<string, string> log = Read(player, Keys.FishLog);
            log[prefab] = (Levels(player, prefab) | Bit(level)).ToString(CultureInfo.InvariantCulture);
            Write(player, Keys.FishLog, log);
        }

        /// <summary>How many species the character has landed at least once.</summary>
        public static int SpeciesCount(Player player) => Read(player, Keys.FishLog).Keys.Count(prefab => Levels(player, prefab) != 0);

        /// <summary>The character's heaviest catch of this fish; false when there is none.</summary>
        public static bool Best(Player player, string prefab, out float weight, out int level)
        {
            weight = 0f;
            level = 0;
            if (!Read(player, Keys.FishRecords).TryGetValue(prefab, out string record))
                return false;
            string[] parts = record.Split(LevelSeparator);
            return parts.Length == 2 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out weight)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out level);
        }

        /// <summary>Records the catch if it is the character's heaviest of this fish; true when it was.</summary>
        public static bool TryRecord(Player player, string prefab, float weight, int level)
        {
            if (Best(player, prefab, out float best, out _) && best >= weight)
                return false;
            Dictionary<string, string> records = Read(player, Keys.FishRecords);
            records[prefab] = weight.ToString("0.0", CultureInfo.InvariantCulture) + LevelSeparator + level.ToString(CultureInfo.InvariantCulture);
            Write(player, Keys.FishRecords, records);
            return true;
        }

        /// <summary>The levels in a mask, lowest first.</summary>
        public static IEnumerable<int> LevelsIn(int mask)
        {
            for (int level = 1; level <= FishInfo.LegendaryLevel; level++)
            {
                if ((mask & Bit(level)) != 0)
                    yield return level;
            }
        }

        private static int Bit(int level) => 1 << (Mathf.Clamp(level, 1, 30) - 1);

        private static Dictionary<string, string> Read(Player player, string key)
        {
            Dictionary<string, string> entries = new Dictionary<string, string>();
            if (player == null || !player.m_customData.TryGetValue(key, out string text) || string.IsNullOrEmpty(text))
                return entries;
            foreach (string entry in text.Split(EntrySeparator))
            {
                int at = entry.IndexOf(ValueSeparator);
                if (at > 0)
                    entries[entry.Substring(0, at)] = entry.Substring(at + 1);
            }
            return entries;
        }

        private static void Write(Player player, string key, Dictionary<string, string> entries) =>
            player.m_customData[key] = string.Join(EntrySeparator.ToString(), entries.Select(pair => pair.Key + ValueSeparator + pair.Value));
    }
}

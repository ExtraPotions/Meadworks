// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// How the world repopulates: camp spawners, dungeon spawners and dungeon loot chests, each with its own timer in
    /// world days. All three are off by default. This feature can quietly undo a player's sense of progress if it runs
    /// too fast, so a camp is worth clearing again after real days rather than hours, and loot is slower still.
    /// </summary>
    public sealed class RespawnRules
    {
        public bool Camps;
        public float CampDays = 5f;
        public bool Dungeons;
        public float DungeonDays = 7f;
        public bool DungeonLoot;
        public float DungeonLootDays = 14f;

        /// <summary>
        /// The spawner timer in the game's own unit, minutes of the world clock (30 to a world day), or 0 when this kind
        /// of spawner is left alone. Up to 3.18.2 a day was counted as 1440 of them, 48 world days.
        /// </summary>
        public float MinutesFor(bool inDungeon)
        {
            float dayMinutes = (float)(NetTime.DaySeconds() / 60.0);
            if (inDungeon)
            {
                return Dungeons ? Mathf.Max(0f, DungeonDays) * dayMinutes : 0f;
            }
            return Camps ? Mathf.Max(0f, CampDays) * dayMinutes : 0f;
        }

        public RespawnRules Clone()
        {
            return new RespawnRules
            {
                Camps = Camps, CampDays = CampDays, Dungeons = Dungeons,
                DungeonDays = DungeonDays, DungeonLoot = DungeonLoot, DungeonLootDays = DungeonLootDays,
            };
        }
    }
}

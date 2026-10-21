// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Rewards/BonusItems.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A landed fish's bonus item. Every fish in the game has an extra-drop table (Fish.m_extraDrops: a Perch stone or
    /// amber, a Tetra obsidian or up to 15 coins, a Magmafish flametal ore, a Surtling core or grausten) that
    /// FishingFloat.Catch rolls once into the inventory, with a 20% chance and one item. For the local player's catch
    /// (<see cref="CatchHook"/>), on that one fish and for that one call:
    /// <list type="bullet">
    /// <item>the chance grows from the fish's own at level 0 to Bonus Item Chance At 100, never below the fish's own;</item>
    /// <item>from Double Bonus Level a roll can bring two items instead of one;</item>
    /// <item>a legendary fish always brings its bonus, and can bring two.</item>
    /// </list>
    /// The table is put back afterwards (<see cref="Restore"/>), and the game names what came in its catch message.
    /// </summary>
    public static class BonusItems
    {
        public static void Prepare(CatchInfo info)
        {
            DropTable table = info.Fish.m_extraDrops;
            if (table == null || table.IsEmpty())
                return;
            info.BonusTable = table;
            info.BonusChance = table.m_dropChance;
            info.BonusMax = table.m_dropMax;
            float level = FishSkill.Of(info.Player);
            float raised = Mathf.Lerp(table.m_dropChance, FishSkill.Percent(FishingCatchSettings.BonusItemChanceAt100.Value), FishSkill.Factor(level));
            table.m_dropChance = info.Legendary ? 1f : Mathf.Max(table.m_dropChance, raised);
            if (info.Legendary || FishSkill.Reached(level, FishingCatchSettings.DoubleBonusLevel.Value))
                table.m_dropMax = Mathf.Max(table.m_dropMax, table.m_dropMin + 1);
        }

        public static void Restore(CatchInfo info)
        {
            if (info.BonusTable == null)
                return;
            info.BonusTable.m_dropChance = info.BonusChance;
            info.BonusTable.m_dropMax = info.BonusMax;
            info.BonusTable = null;
        }
    }
}

// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the kinds of design section 1.1 C and the clock each one runs on (B1).
namespace ExtraPotions.Delve.Engine
{
    /// <summary>What a delay is measured in (design B1): world days since the trigger, or wall-clock hours (UTC).</summary>
    public enum ClockKind { InGameDays, RealHours }

    /// <summary>Every kind the regeneration engine brings back, in config file order.</summary>
    public enum DelveKind
    {
        /// <summary>C1: wipe and regenerate a dungeon interior.</summary>
        FullReset,
        /// <summary>C2: loot chest refill.</summary>
        Chests,
        /// <summary>C3: sunken crypt muddy scrap piles.</summary>
        MudPiles,
        /// <summary>C3: frost cave crystals.</summary>
        FrostCaveCrystals,
        /// <summary>C3: infested mine black core stands.</summary>
        BlackCores,
        /// <summary>C3: Morkhalla gems, coin rubble, treasure piles and the black ice block.</summary>
        Morkhalla,
        /// <summary>C3: Hole trash piles and spawner nests.</summary>
        Hole,
        /// <summary>C4: Mistlands ancient armor.</summary>
        AncientArmor,
        /// <summary>C4: Mountain obsidian.</summary>
        Obsidian,
        /// <summary>C4: Ashlands flametal spires.</summary>
        Flametal,
        /// <summary>C4: Deep North ice ponds and shore ice.</summary>
        Ice,
        /// <summary>C5: the Queen's return to a citadel.</summary>
        Queen,
        /// <summary>DV15: creatures of cleared camps (one-shot spawners outside interiors).</summary>
        CampSpawners,
        /// <summary>DV15: creatures of cleared dungeons (one-shot spawners inside interiors).</summary>
        DungeonSpawners,
        /// <summary>Kall Fimbulbringer's return to the Deep North boss room (added last so stored kind numbers keep their meaning).</summary>
        Kall,
    }
}

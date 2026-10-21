// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/RockBreak.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>What broke a chunk (<see cref="RockBreak.Cause"/>).</summary>
    public enum BreakCause
    {
        /// <summary>A miner's pickaxe hit, sent by their swing (an intact deposit's first hit included).</summary>
        Hit = 0,

        /// <summary>Splash damage from a miner's hit on a touching chunk (a Pickaxes hit applied directly on the owner).</summary>
        Splash = 1,

        /// <summary>The chunk lost its support when a miner's hit or splash broke the chunks under it (the game's support check).</summary>
        Collapse = 2,
    }

    /// <summary>
    /// A chunk of a rock breaking, or a single-piece rock breaking, on the rock's owner (<see cref="MineBreak"/>).
    /// Everything is read before the game can destroy the rock's ZDO (the last chunk of a rock destroys it). Break
    /// features add <see cref="ExtraRolls"/>; the foundation spawns them afterwards, once, as the game spawns this
    /// chunk's drops.
    /// </summary>
    public sealed class RockBreak
    {
        /// <summary>Never more extra rolls than this per break, whatever the settings add up to.</summary>
        public const int MaxExtraRolls = 50;

        public Rock Rock { get; set; }

        /// <summary>The rock's ZDOID, read before the break (the rock may be gone once its last chunk broke).</summary>
        public ZDOID RockId { get; set; }

        /// <summary>The chunk's area index; -1 for a single-piece rock.</summary>
        public int Chunk { get; set; }

        /// <summary>
        /// Where the game spawned this break's drops, and where extra rolls and finds go: a MineRock5 chunk's centre
        /// (its hit point when the rock's m_hitEffectAreaCenter is off), a MineRock's hit point pulled 0.2 m back along the
        /// hit, a single piece's position on the ground raised by its DropOnDestroyed's m_spawnYOffset.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>A single piece stacks its drops this far apart upwards (DropOnDestroyed.m_spawnYStep); 0 for chunks, which scatter them.</summary>
        public float StackStep { get; set; }

        /// <summary>The chunk's own drop table (MineRock5/MineRock m_dropItems, a piece's DropOnDestroyed table).</summary>
        public DropTable Drops { get; set; }

        /// <summary>The breaking hit as the game handled it (after resistances); for a collapse, the game's structural hit.</summary>
        public HitData Hit { get; set; }

        /// <summary>The miner the break counts for: of the hit, of the splash, or of the hit whose support check dropped the chunk. Never null.</summary>
        public Miner Miner { get; set; }

        public BreakCause Cause { get; set; }

        /// <summary>The miner carries a cheated item: extra drops are marked cheated like the game's own.</summary>
        public bool Cheated { get; set; }

        public bool IsOre => Rock.IsOre;
        public string Kind => Rock.Kind;
        public Heightmap.Biome Biome => Rock.Biome;

        /// <summary>Extra rolls of <see cref="Drops"/> the break features asked for so far.</summary>
        public int ExtraRolls { get; private set; }

        /// <summary>Adds whole extra rolls of the chunk's drop table.</summary>
        public void AddRolls(int rolls)
        {
            if (rolls > 0)
                ExtraRolls = Mathf.Min(MaxExtraRolls, ExtraRolls + rolls);
        }

        /// <summary>Adds extra rolls with a fraction: the whole rolls, plus one more with the fraction as its chance (1.25: one, and a 25% chance of a second).</summary>
        public void AddRolls(float rolls)
        {
            if (rolls <= 0f)
                return;
            rolls = Mathf.Min(rolls, MaxExtraRolls);
            int whole = Mathf.FloorToInt(rolls);
            AddRolls(whole + (Random.value < rolls - whole ? 1 : 0));
        }
    }
}

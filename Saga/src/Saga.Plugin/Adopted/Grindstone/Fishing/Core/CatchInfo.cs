// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Core/CatchInfo.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A fish being landed, captured before FishingFloat.Catch picks it up (the pickup destroys the fish's object): who
    /// caught it, what it is, how big, and its weight on the scale (<see cref="CatchWeight"/>). Features fill in what
    /// they found (a new record) and what they changed (the bonus-item table, put back afterwards).
    /// </summary>
    public sealed class CatchInfo
    {
        public CatchInfo(Player player, Fish fish, FishingFloat fishingFloat)
        {
            Player = player;
            Fish = fish;
            Float = fishingFloat;
            Prefab = FishInfo.Prefab(fish);
            Name = FishInfo.Name(fish);
            Level = FishInfo.Level(fish);
            SpeciesScale = FishInfo.SpeciesScale(fish);
            Position = fish.transform.position;
            ItemDrop item = FishInfo.Item(fish);
            Weight = item == null ? 0f : CatchWeight.Roll(item.m_itemData.m_shared, Level);
        }

        public Player Player { get; }
        public Fish Fish { get; }

        /// <summary>The float that landed it, or null when the catch came from outside a float's step.</summary>
        public FishingFloat Float { get; }

        public string Prefab { get; }
        public string Name { get; }
        public int Level { get; }
        public float SpeciesScale { get; }
        public float Weight { get; }
        public Vector3 Position { get; }

        public bool Legendary => FishInfo.IsLegendary(Level);

        /// <summary>The catch beat the angler's record for the species (not set for the first catch of one).</summary>
        public bool NewRecord { get; set; }

        // The fish's bonus-item table as it was, while BonusItems has raised it.
        internal DropTable BonusTable;
        internal float BonusChance;
        internal int BonusMax;
    }
}

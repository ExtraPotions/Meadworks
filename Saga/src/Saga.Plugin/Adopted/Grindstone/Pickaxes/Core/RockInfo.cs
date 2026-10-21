// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Core/RockInfo.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// What is known about a kind of rock from its prefab, the same on every machine (<see cref="RockCatalog"/> builds
    /// and caches it per prefab name). A <see cref="Rock"/> adds the instance: its position, biome and network view.
    /// </summary>
    public sealed class RockInfo
    {
        /// <summary>The prefab name, for example "rock4_copper_frac".</summary>
        public string Prefab { get; set; }

        /// <summary>
        /// The kind of rock: the prefab name without "_frac". An intact deposit (a Destructible that turns into a rock
        /// when hit) takes its fractured form's kind, so "rock4_copper" and "rock4_copper_frac" are both
        /// "rock4_copper", and "mudpile_beacon" is "mudpile".
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// The name as the game shows it, a localization token such as "$piece_deposit_copper". A rock without one goes by
        /// its first ore item's token ("$item_ice" for the ice rocks, "$item_chitin" for a Leviathan), a plain stone rock
        /// without one by its prefab name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The deposit as a player tells it apart, for discoveries: its <see cref="Name"/> when that is a localization
        /// token, else its <see cref="Kind"/>. Kinds that look and read the same share it (mudpile, mudpile2 and
        /// mudpile_old; both giant helmets; IceShard_01 to 06), so each is discovered once.
        /// </summary>
        public string Identity => Name != null && Name.StartsWith("$") ? Name : Kind;

        /// <summary>
        /// An ore deposit: its drop table holds an item that is not plain stone ("Plain Stone Items"). For an intact
        /// deposit, the table of the rock it turns into.
        /// </summary>
        public bool IsOre { get; set; }

        /// <summary>The tool tier a hit needs to damage it (m_minToolTier).</summary>
        public int Tier { get; set; }

        /// <summary>
        /// One chunk's full health in the prefab, before the world level adds to it (m_health: copper 50, mud piles 5, a
        /// Leviathan's pieces 40); a single piece's own (1 for an intact deposit, which never breaks as itself).
        /// </summary>
        public float Health { get; set; }

        /// <summary>It breaks chunk by chunk: a MineRock5 or a MineRock. False for a single piece (tin, obsidian) and for an intact deposit.</summary>
        public bool HasChunks { get; set; }

        /// <summary>It is an intact deposit: a Destructible whose m_spawnWhenDestroyed is a rock (its first hit turns it into that).</summary>
        public bool Fractures { get; set; }

        /// <summary>It carries a Beacon, the Wishbone's target, on itself or a child (buried silver veins, some mud piles).</summary>
        public bool HasBeacon { get; set; }
    }
}

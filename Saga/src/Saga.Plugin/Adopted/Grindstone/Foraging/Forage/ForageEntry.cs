// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Foraging/Forage/ForageEntry.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>One item of the Forage file: its prefab name and its experience factor.</summary>
    public sealed class ForageEntry
    {
        public ForageEntry(string item, float experience)
        {
            Item = item;
            Experience = experience;
        }

        /// <summary>The item's prefab name, as the file lists it.</summary>
        public string Item { get; }

        /// <summary>The factor on Experience Per Pick, 0 or more.</summary>
        public float Experience { get; }
    }
}

// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Shepherd.Husbandry
{
    /// <summary>
    /// The Husbandry skill's identity in the CS1 skill registry (design "CS1 note"): Crucible's GrindstoneSkills map
    /// turns <c>grindstone_husbandry</c> into <c>meadworks_husbandry</c>, imported once per character, and the level is
    /// published on the player ZDO under the default <c>&lt;identity&gt;_level</c> key, which animal owners read to find
    /// the best keeper in range.
    /// </summary>
    public static class HusbandryIdentity
    {
        public const string Identity = "meadworks_husbandry";
        public const string PublishKey = "meadworks_husbandry_level";
        public const string ImportFrom = "grindstone_husbandry";
        public const string Name = "Husbandry";
        public const string Description = "Tending animals: taming, feeding, breeding and herd care.";

        /// <summary>Keeper Range default (design 4.4): the best Husbandry level within it drives an animal's perks.</summary>
        public const double DefaultKeeperRange = 30.0;
    }
}

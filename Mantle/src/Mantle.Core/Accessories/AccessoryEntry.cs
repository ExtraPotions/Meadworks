// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace Mantle.Accessories
{
    /// <summary>The two kinds of accessory slot: a ring cell (up to two) and the belt cell.</summary>
    public enum AccessorySlot
    {
        Ring,
        Belt,
    }

    /// <summary>How an accessory is made: its crafting station (prefab name), the station level and the cost as prefab:amount pairs.</summary>
    public sealed class AccessoryRecipe
    {
        public AccessoryRecipe(string station, int level, string cost)
        {
            Station = station;
            Level = level;
            Cost = cost;
        }

        public string Station { get; }
        public int Level { get; }
        public string Cost { get; }
    }

    /// <summary>
    /// One item a ring or belt slot takes, by its prefab name. A slot accepts only an item with an entry (the item's tag):
    /// Mantle's own rings and belts are built-in entries that Mantle also makes and lets be crafted (<see cref="Recipe"/>),
    /// and any other prefab, the game's or another mod's, is tagged by an entry with a slot and its stats and no recipe.
    /// </summary>
    public sealed class AccessoryEntry
    {
        public AccessoryEntry(string id, AccessorySlot slot, AccessoryStats stats, AccessoryRecipe recipe = null, string name = null,
            string look = null, float weight = 0f, bool enabled = true, bool builtIn = false)
        {
            Id = id;
            Slot = slot;
            Stats = stats ?? AccessoryStats.None;
            Recipe = recipe;
            Name = name;
            Look = look;
            Weight = weight;
            Enabled = enabled;
            BuiltIn = builtIn;
        }

        /// <summary>The item's prefab name ("Mantle_BoneBand", "BeltStrength").</summary>
        public string Id { get; }

        public AccessorySlot Slot { get; }

        public AccessoryStats Stats { get; }

        /// <summary>How it is crafted; null for a tagged item that is not Mantle's.</summary>
        public AccessoryRecipe Recipe { get; }

        /// <summary>English name and look of one of Mantle's own items; null when tagged.</summary>
        public string Name { get; }
        public string Look { get; }
        public float Weight { get; }

        /// <summary>A disabled entry tags nothing: the item is an ordinary item again (and, if Mantle's own, cannot be crafted).</summary>
        public bool Enabled { get; }

        /// <summary>One of Mantle's own items (built into the game when it starts), as opposed to a tagged prefab.</summary>
        public bool BuiltIn { get; }

        public AccessoryEntry With(AccessoryOverride change)
        {
            AccessoryRecipe recipe = Recipe;
            if (BuiltIn && (change.Station != null || change.Level.HasValue || change.Cost != null))
                recipe = new AccessoryRecipe(change.Station ?? Recipe.Station, change.Level ?? Recipe.Level, change.Cost ?? Recipe.Cost);
            return new AccessoryEntry(Id, BuiltIn ? Slot : change.Slot ?? Slot, change.ApplyTo(Stats), recipe, Name, Look, Weight,
                change.Enabled ?? Enabled, BuiltIn);
        }
    }

    /// <summary>
    /// One entry of Mantle.Accessories.yml, every key optional: over a known entry it changes only what it names; for an
    /// unknown prefab it tags that item and needs a slot.
    /// </summary>
    public sealed class AccessoryOverride
    {
        public string Id { get; set; }
        public AccessorySlot? Slot { get; set; }
        public bool? Enabled { get; set; }
        public string Station { get; set; }
        public int? Level { get; set; }
        public string Cost { get; set; }
        public float? MaxHealth { get; set; }
        public float? MaxStamina { get; set; }
        public float? MaxEitr { get; set; }
        public float? StaminaRegenPercent { get; set; }
        public float? MoveSpeedPercent { get; set; }
        public float? AttackStaminaPercent { get; set; }
        public float? Carry { get; set; }

        public AccessoryStats ApplyTo(AccessoryStats stats) => new AccessoryStats(MaxHealth ?? stats.MaxHealth, MaxStamina ?? stats.MaxStamina,
            MaxEitr ?? stats.MaxEitr, StaminaRegenPercent ?? stats.StaminaRegenPercent, MoveSpeedPercent ?? stats.MoveSpeedPercent,
            AttackStaminaPercent ?? stats.AttackStaminaPercent, Carry ?? stats.Carry);
    }
}

// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: EliteCreaturesPack 0.7.2 at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (items, game bases, damage factor, recipes and stations of
// Arsenal/ArsenalWeapon.cs, ArsenalItems.cs, ArsenalRecipes.cs, ArsenalArrow.cs, Crossbow/XbowItem.cs, XbowBolts.cs,
// Headsman/Greataxe/GreataxeItems.cs, GreataxeRecipe.cs, Kraken/Loot/KrakenLoot.cs, KrakenMeat.cs, ShieldRecipe.cs).
// Changed by expDARE, 2026: restated as a Unity-free data table (design 1.1 B); settings referenced by id.
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Bestiary.Catalog
{
    public enum ItemKind
    {
        Weapon,
        Ammo,
        Material,
        Food,
        Shield,
    }

    /// <summary>Crafting station prefab names.</summary>
    public static class Stations
    {
        public const string Workbench = "piece_workbench";
        public const string Forge = "forge";
    }

    /// <summary>Food values of a consumable (health, stamina, healing per tick, seconds).</summary>
    public readonly struct FoodStats
    {
        public FoodStats(float health, float stamina, float regen, float burnTime)
        {
            Health = health;
            Stamina = stamina;
            Regen = regen;
            BurnTime = burnTime;
        }

        public float Health { get; }
        public float Stamina { get; }
        public float Regen { get; }
        public float BurnTime { get; }

        public override string ToString() => $"{Health}/{Stamina}/{Regen}/{BurnTime}s";
    }

    /// <summary>
    /// A recipe: station, level, amount made and cost text (<c>item:amount[:per upgrade]</c>, comma separated); each
    /// may instead come from a setting by id. <see cref="EnabledSetting"/> switches it on or off.
    /// </summary>
    public sealed class RecipeSpec
    {
        public RecipeSpec(string station, int level, int amount, string? cost, string? costSetting = null,
            string? levelSetting = null, string? amountSetting = null, string? enabledSetting = null)
        {
            Station = station;
            Level = level;
            Amount = amount;
            Cost = cost;
            CostSetting = costSetting;
            LevelSetting = levelSetting;
            AmountSetting = amountSetting;
            EnabledSetting = enabledSetting;
        }

        public string Station { get; }
        public int Level { get; }
        public int Amount { get; }

        /// <summary>Fixed cost text, or null when it is computed (<see cref="Recipes.GreataxeCost"/>) or read from <see cref="CostSetting"/>.</summary>
        public string? Cost { get; }

        public string? CostSetting { get; }
        public string? LevelSetting { get; }
        public string? AmountSetting { get; }
        public string? EnabledSetting { get; }
    }

    /// <summary>One item of design 1.1 B: a runtime copy of a game item, renamed to its ECP prefab name (Q2).</summary>
    public sealed class ItemDefinition
    {
        public ItemDefinition(string prefab, string word, string baseItem, ItemKind kind, BestiaryModule module,
            float damageFactor = 1f, RecipeSpec? recipe = null, FoodStats food = default)
        {
            Prefab = prefab;
            Word = word;
            BaseItem = baseItem;
            Kind = kind;
            Module = module;
            DamageFactor = damageFactor;
            Recipe = recipe;
            Food = food;
        }

        public string Prefab { get; }

        /// <summary>Localisation key of the name; the description key adds <c>_description</c>.</summary>
        public string Word { get; }

        /// <summary>The game item copied at load: its handling, skill, sounds, durability, weight and upgrades.</summary>
        public string BaseItem { get; }

        public ItemKind Kind { get; }
        public BestiaryModule Module { get; }

        /// <summary>Multiplies the base item's damage and damage per level (bone weapons 0.85).</summary>
        public float DamageFactor { get; }

        public RecipeSpec? Recipe { get; }
        public FoodStats Food { get; }
    }

    public static class ItemCatalog
    {
        /// <summary>Bone weapons are like bronze, a little weaker (ECP <c>ArsenalItems.DamageFactor</c>).</summary>
        public const float BoneDamageFactor = 0.85f;

        /// <summary>The bronze-age flint and bone kit level (ECP <c>ArsenalRecipes.WeaponLevel</c>).</summary>
        public const int BoneWeaponLevel = 2;

        private static readonly (string Key, string GameItem, string Cost)[] Weapons =
        {
            ("Dagger", "KnifeCopper", "BoneFragments:8:4"),
            ("Sword", "SwordBronze", "ECP_Spine:1, BoneFragments:10:5"),
            ("Axe", "AxeBronze", "ECP_Spine:1, BoneFragments:10:5"),
            ("Mace", "MaceBronze", "ECP_Spine:1, BoneFragments:12:6"),
            ("Spear", "SpearBronze", "ECP_Spine:1, BoneFragments:10:5"),
            ("Atgeir", "AtgeirBronze", "ECP_Spine:2, BoneFragments:16:8"),
            ("Bow", "BowFineWood", "ECP_Spine:1, BoneFragments:12:6"),
        };

        public static IReadOnlyList<ItemDefinition> All { get; } = Build();

        public static IReadOnlyList<ItemDefinition> BoneWeapons { get; } = All.Where(i => i.Prefab.StartsWith("ECP_Bone") && i.Kind == ItemKind.Weapon && i.Prefab != "ECP_BoneCrossbow").ToArray();

        public static ItemDefinition? Find(string prefab) => All.FirstOrDefault(i => i.Prefab == prefab);

        private static ItemDefinition[] Build()
        {
            const string bow = "7 - Bone Crossbow.", axe = "26 - Executioner Greataxe.";
            var list = Weapons.Select(w => new ItemDefinition("ECP_Bone" + w.Key, "item_ecp_bone" + w.Key.ToLowerInvariant(), w.GameItem,
                ItemKind.Weapon, BestiaryModule.ArsenalCrafting, BoneDamageFactor,
                new RecipeSpec(Stations.Workbench, BoneWeaponLevel, 1, w.Cost))).ToList();
            list.Add(new ItemDefinition("ECP_Spine", "item_ecp_spine", "BoneFragments", ItemKind.Material, BestiaryModule.Skeletons));
            list.Add(new ItemDefinition("ECP_ArrowBone", "item_ecp_arrowbone", "ArrowWood", ItemKind.Ammo, BestiaryModule.ArsenalCrafting,
                recipe: new RecipeSpec(Stations.Workbench, 2, 20, "BoneFragments:8")));
            list.Add(new ItemDefinition("ECP_BoneCrossbow", "item_ecp_bonecrossbow", "CrossbowArbalest", ItemKind.Weapon, BestiaryModule.ArsenalCrafting,
                recipe: new RecipeSpec(Stations.Workbench, 2, 1, null, bow + "Recipe", bow + "Workbench Level", enabledSetting: bow + "Craftable")));
            list.Add(new ItemDefinition("ECP_BoltBoneBlunt", "item_ecp_boltboneblunt", "BoltBone", ItemKind.Ammo, BestiaryModule.ArsenalCrafting,
                recipe: new RecipeSpec(Stations.Workbench, 2, 20, null, bow + "Bolt Recipe", bow + "Workbench Level", bow + "Bolts Per Craft", bow + "Craftable")));
            list.Add(new ItemDefinition("ECP_ExecutionerGreataxe", "item_ecp_executionergreataxe", "Battleaxe", ItemKind.Weapon, BestiaryModule.ArsenalCrafting,
                recipe: new RecipeSpec(Stations.Workbench, 3, 1, null, levelSetting: axe + "Workbench Level", enabledSetting: axe + "Recipe")));
            list.Add(new ItemDefinition("ECP_ExecutionerAxehead", "item_ecp_executioneraxehead", "BoneFragments", ItemKind.Material, BestiaryModule.Crypt));
            list.Add(new ItemDefinition("ECP_KrakenBeak", "item_ecp_krakenbeak", "SerpentScale", ItemKind.Material, BestiaryModule.Sea));
            list.Add(new ItemDefinition("ECP_KrakenMeat", "item_ecp_krakenmeat", "SerpentMeat", ItemKind.Material, BestiaryModule.Sea));
            list.Add(new ItemDefinition("ECP_KrakenMeatCooked", "item_ecp_krakenmeatcooked", "SerpentMeatCooked", ItemKind.Food, BestiaryModule.Sea,
                food: new FoodStats(80f, 28f, 4f, 1800f)));
            list.Add(new ItemDefinition("ECP_ShieldKraken", "item_ecp_shieldkraken", "ShieldSilver", ItemKind.Shield, BestiaryModule.Sea,
                recipe: new RecipeSpec(Stations.Forge, 3, 1, "FineWood:10:10, Silver:5:3, ECP_KrakenBeak:1")));
            return list.ToArray();
        }
    }
}

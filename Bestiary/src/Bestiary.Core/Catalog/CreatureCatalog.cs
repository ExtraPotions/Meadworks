// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, GPL-3.0-or-later: EliteCreaturesPack 0.7.2 at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 (creature prefabs, game bases, scales and drop tables of Mimic/, Slinger/,
// RimeGiant/, Kraken/, Arsenal/, Crossbow/ and Headsman/).
// Changed by expDARE, 2026: restated as a Unity-free data table (design 1.1 A); health read from settings by id.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Spawning;

namespace ExtraPotions.Bestiary.Catalog
{
    /// <summary>Feature modules with master switches (design 1.2).</summary>
    public enum BestiaryModule
    {
        Crypt,
        Forest,
        Mountain,
        Sea,
        Skeletons,
        ArsenalCrafting,
        Workshop,

        /// <summary>Draugr Hook, Fuling Spearman, Slag Charred and Marrow Lantern, each a copy of a game body.</summary>
        Kit,
    }

    /// <summary>How a creature enters the world (design 1.1 A, section 8 "Spawn swaps", "Spawn entries").</summary>
    public enum SpawnKind
    {
        /// <summary>A crypt chest becomes a dormant mimic when its crypt first fills.</summary>
        MimicSwap,

        /// <summary>A share of greydwarf spawns in listed biomes, wild and from nests.</summary>
        GreydwarfShare,

        /// <summary>One even draw among the plain skeleton and the enabled arsenal kinds.</summary>
        SkeletonDraw,

        /// <summary>Own entry in every zone's spawn system (Rime Giant, Kraken).</summary>
        ZoneEntry,

        /// <summary>A creature spawner placed in a burial chamber.</summary>
        ChamberSpawner,

        /// <summary>Raised by the Executioner in a fight.</summary>
        Raised,

        /// <summary>A share of one wild game creature. Spawners, piles, raids and elites stay the game's.</summary>
        WildShare,

        /// <summary>One per biome region, claimed until Delve clears it.</summary>
        RegionClaim,

        /// <summary>A roll on an existing creature spawner, which still spawns its own creature the rest of the time.</summary>
        SpawnerRoll,
    }

    /// <summary>One <c>CharacterDrop.Drop</c>: item prefab, amount range as passed to the game, chance and scaling.</summary>
    public sealed class DropSpec
    {
        public DropSpec(string item, int min, int max, float chance = 1f, bool levelMultiplier = true, bool dontScale = false, string? chanceSetting = null)
        {
            Item = item;
            Min = min;
            Max = max;
            Chance = chance;
            LevelMultiplier = levelMultiplier;
            DontScale = dontScale;
            ChanceSetting = chanceSetting;
        }

        public string Item { get; }
        public int Min { get; }
        public int Max { get; }

        /// <summary>0 to 1; replaced by <see cref="ChanceSetting"/> (a percent setting) when that is set.</summary>
        public float Chance { get; }

        public bool LevelMultiplier { get; }

        /// <summary>Not scaled by the world's resource rate.</summary>
        public bool DontScale { get; }

        /// <summary>Setting id whose percent value is the chance, or null.</summary>
        public string? ChanceSetting { get; }
    }

    /// <summary>One creature of design 1.1 A: a runtime copy of a game body, renamed to its ECP prefab name (Q2).</summary>
    public sealed class CreatureDefinition
    {
        public CreatureDefinition(string prefab, string? word, string basePrefab, BestiaryModule module, SpawnKind spawn,
            IReadOnlyList<DropSpec> drops, bool keepBaseDrops, float scale = 1f, string? healthSetting = null,
            bool boss = false, bool dropsChestLoot = false, bool worthless = false)
        {
            Prefab = prefab;
            Word = word;
            BasePrefab = basePrefab;
            Module = module;
            Spawn = spawn;
            Drops = drops;
            KeepBaseDrops = keepBaseDrops;
            Scale = scale;
            HealthSetting = healthSetting;
            Boss = boss;
            DropsChestLoot = dropsChestLoot;
            Worthless = worthless;
        }

        /// <summary>Prefab name, hashed into worlds: never renamed.</summary>
        public string Prefab { get; }

        /// <summary>Localisation key of the creature's name, or null to keep the base body's name.</summary>
        public string? Word { get; }

        /// <summary>The game prefab copied at load (<c>ZNetScene.GetPrefab</c>).</summary>
        public string BasePrefab { get; }

        public BestiaryModule Module { get; }
        public SpawnKind Spawn { get; }

        /// <summary>Drops added to (or replacing) the base body's.</summary>
        public IReadOnlyList<DropSpec> Drops { get; }

        /// <summary>True: <see cref="Drops"/> are added to the base body's; false: they replace them.</summary>
        public bool KeepBaseDrops { get; }

        /// <summary>Uniform scale of the copy.</summary>
        public float Scale { get; }

        /// <summary>Setting id of the creature's health, or null to keep the base body's.</summary>
        public string? HealthSetting { get; }

        /// <summary>Shows the game's boss bar while it fights (the Kraken only; the giant and the Executioner have none).</summary>
        public bool Boss { get; }

        /// <summary>Drops the loot of the chest it replaced instead of a drop table (mimic).</summary>
        public bool DropsChestLoot { get; }

        /// <summary>Drops nothing and pays no runes (Wildblood and Inscribe read it).</summary>
        public bool Worthless { get; }
    }

    /// <summary>Every Bestiary creature, in design order.</summary>
    public static class CreatureCatalog
    {
        private const string Skeleton = "Skeleton";

        /// <summary>The seven arsenal skeleton titles with their weapon key (ECP <c>ArsenalWeapon.All</c>).</summary>
        public static readonly IReadOnlyList<(string Title, string Weapon)> ArsenalTitles = new[]
        {
            ("Cutthroat", "Dagger"), ("Swordsman", "Sword"), ("Axeman", "Axe"), ("Bonebreaker", "Mace"),
            ("Spearman", "Spear"), ("Halberdier", "Atgeir"), ("Bowman", "Bow"),
        };

        private static DropSpec Spine() => new DropSpec("ECP_Spine", 1, 1, 0.1f, levelMultiplier: false);

        public static IReadOnlyList<CreatureDefinition> All { get; } = Build();

        public static IReadOnlyList<CreatureDefinition> ArsenalSkeletons { get; } =
            All.Where(c => ArsenalTitles.Any(t => c.Prefab == "ECP_Skeleton" + t.Title)).ToArray();

        public static CreatureDefinition? Find(string prefab) => All.FirstOrDefault(c => c.Prefab == prefab);

        private static CreatureDefinition[] Build()
        {
            var list = new List<CreatureDefinition>
            {
                new CreatureDefinition("ECP_CryptMimic", "enemy_ecp_cryptmimic", Skeleton, BestiaryModule.Crypt, SpawnKind.MimicSwap,
                    Array.Empty<DropSpec>(), keepBaseDrops: false, dropsChestLoot: true),
                new CreatureDefinition("ECP_GreydwarfSlinger", "enemy_ecp_greydwarfslinger", "Greydwarf", BestiaryModule.Forest, SpawnKind.GreydwarfShare,
                    new[] { new DropSpec("Stone", 2, 4) }, keepBaseDrops: true),
                new CreatureDefinition("ECP_RimeGiant", "enemy_ecp_rimegiant", "Troll", BestiaryModule.Mountain, SpawnKind.ZoneEntry,
                    new[] { new DropSpec("Crystal", 4, 8), new DropSpec("FreezeGland", 3, 6), new DropSpec("SilverOre", 3, 6) },
                    keepBaseDrops: false, scale: 1.4f, healthSetting: "4 - Rime Giant.Health"),
                new CreatureDefinition("ECP_Kraken", "enemy_ecp_kraken", "Serpent", BestiaryModule.Sea, SpawnKind.ZoneEntry,
                    new[]
                    {
                        new DropSpec("Chitin", 20, 30), new DropSpec("ECP_KrakenMeat", 5, 8), new DropSpec("Coins", 150, 300),
                        new DropSpec("ECP_KrakenBeak", 1, 2, levelMultiplier: false, dontScale: true),
                    },
                    keepBaseDrops: false, healthSetting: "5 - Kraken.Health", boss: true),
            };
            foreach (var (title, _) in ArsenalTitles)
                list.Add(new CreatureDefinition("ECP_Skeleton" + title, "enemy_ecp_skeleton" + title.ToLowerInvariant(), Skeleton,
                    BestiaryModule.Skeletons, SpawnKind.SkeletonDraw, new[] { Spine() }, keepBaseDrops: true));
            list.Add(new CreatureDefinition("ECP_SkeletonCrossbowman", "enemy_ecp_skeletoncrossbowman", Skeleton, BestiaryModule.Skeletons,
                SpawnKind.SkeletonDraw, new[] { Spine(), new DropSpec("ECP_BoltBoneBlunt", 1, 3, 0.5f) }, keepBaseDrops: true));
            list.Add(new CreatureDefinition("ECP_Headsman", "enemy_ecp_headsman", Skeleton, BestiaryModule.Crypt, SpawnKind.ChamberSpawner,
                new[]
                {
                    new DropSpec("Coins", 20, 40, levelMultiplier: false), new DropSpec("BoneFragments", 4, 8, levelMultiplier: false),
                    new DropSpec("ECP_ExecutionerAxehead", 1, 1, 0.5f, levelMultiplier: false, chanceSetting: "25 - Crypt Executioner.Axehead Chance"),
                },
                keepBaseDrops: false, scale: 1.25f, healthSetting: "25 - Crypt Executioner.Health"));
            list.Add(new CreatureDefinition("ECP_HeadsmanSkeleton", null, "Skeleton_NoArcher", BestiaryModule.Crypt, SpawnKind.Raised,
                Array.Empty<DropSpec>(), keepBaseDrops: false, worthless: true));
            list.Add(new CreatureDefinition(KitCreatures.BogHook, "enemy_mw_boghook", KitCreatures.Draugr, BestiaryModule.Kit, SpawnKind.WildShare,
                Array.Empty<DropSpec>(), keepBaseDrops: true));
            list.Add(new CreatureDefinition(KitCreatures.Javelineer, "enemy_mw_fulingjavelineer", KitCreatures.Fuling, BestiaryModule.Kit, SpawnKind.WildShare,
                Array.Empty<DropSpec>(), keepBaseDrops: true));
            list.Add(new CreatureDefinition(KitCreatures.SlagCharred, "enemy_mw_slagcharred", KitCreatures.Charred, BestiaryModule.Kit, SpawnKind.RegionClaim,
                Array.Empty<DropSpec>(), keepBaseDrops: true, healthSetting: "43 - Slag Charred.Health"));
            list.Add(new CreatureDefinition(KitCreatures.MarrowLantern, "enemy_mw_marrowlantern", KitCreatures.DeepNorthSkeleton, BestiaryModule.Kit, SpawnKind.SpawnerRoll,
                new[]
                {
                    new DropSpec("FreezeGland", 1, 1, levelMultiplier: false, dontScale: true),
                    new DropSpec("Coins", 8, 20),
                },
                keepBaseDrops: false));
            return list.ToArray();
        }
    }
}

// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/Keys.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Every ZDO key, RPC name and player custom-data key GrindstoneSkills uses, in one place. All are GrindstoneSkills' own
    /// names, prefixed so they never meet another mod's.
    /// </summary>
    public static class Keys
    {
        // Cooking station slots, one set per slot index: append the slot number.
        /// <summary>long: player ID of the cook who put the food on.</summary>
        public const string SlotCook = "saga.cook";
        /// <summary>float: that cook's Cooking level when the food went on.</summary>
        public const string SlotLevel = "saga.level";
        /// <summary>float: stars of the raw input (dough, unbaked pies); 0 for plain raw food.</summary>
        public const string SlotInput = "saga.input";
        /// <summary>int: stars rolled when the dish turned done; -1 while not rolled.</summary>
        public const string SlotStars = "saga.stars";

        // Fermenter, one set per barrel.
        /// <summary>int: stars of the mead base in the barrel.</summary>
        public const string BaseStars = "saga.base_stars";
        /// <summary>float: Cooking level of the player who put the base in.</summary>
        public const string BaseLevel = "saga.base_level";

        /// <summary>int 0..3 on a kitchen's ZDO: dishes below this many stars are thrown away.</summary>
        public const string MinStars = "saga.min_stars";

        // RPCs. Payloads with more than four values travel as one ZPackage, written and read in the listed order.
        /// <summary>On a cooking station, to the owner. ZPackage: string item, bool cheated, float inputStars, float cookLevel, long cookPlayerId.</summary>
        public const string RpcAddItem = "saga.AddItem";
        /// <summary>On a fermenter, to the owner. ZPackage: int nameHash, bool cheated, int baseStars, float cookLevel.</summary>
        public const string RpcAddBase = "saga.AddBase";
        /// <summary>On a kitchen, to the owner: (int minStars).</summary>
        public const string RpcSetFilter = "saga.SetFilter";
        /// <summary>Routed RPC to everybody: (long cookPlayerId, string dishPrefab); only that player's client acts on it.</summary>
        public const string RpcCookCredit = "saga.CookCredit";

        // Player custom data (Player.m_customData, saved with the character).
        /// <summary>Prefix; append the food's prefab name. Value: the stars of that active food.</summary>
        public const string ActiveFood = "saga.food_";
        /// <summary>Comma-separated prefab names of every dish the character has made.</summary>
        public const string MadeDishes = "saga.made";

        // Sailing.
        /// <summary>float on a player's own ZDO, written by that player's client: their Sailing level.</summary>
        public const string SailingLevel = "saga.sailing_level";
        /// <summary>float on a ship's ZDO: the Sailing level of the player who built it, written when it is placed.</summary>
        public const string ShipwrightLevel = "saga.shipwright";
        /// <summary>On a ship, to everybody who has it loaded: () - a lookout pulse goes out from the ship.</summary>
        public const string RpcLookout = "saga.Lookout";
        /// <summary>On a ship, to everybody who has it loaded: (Vector3 direction, long callerPlayerId) - a player aboard called the wind; the owner stores it, the crew are told.</summary>
        public const string RpcWindCall = "saga.WindCall";
        /// <summary>Vector3 on a ship's ZDO, written by its owner: the flat direction the called wind blows toward.</summary>
        public const string WindCallDirection = "saga.wind_dir";
        /// <summary>long on a ship's ZDO, written by its owner: world time (ticks) when the called wind ends.</summary>
        public const string WindCallUntil = "saga.wind_until";

        /// <summary>The Sailing skill's identity: its SkillType number is this name's stable hash.</summary>
        public const string SailingSkillName = "saga.sailing";

        // Woodcutting. Log keys are written by the log's owner when it spawns; halves inherit them from their log.
        /// <summary>long on a log's ZDO: player ID of the woodcutter who felled its tree (0 when unknown).</summary>
        public const string WoodPlayer = "saga.wood_player";
        /// <summary>float on a log's ZDO: that woodcutter's Woodcutting level when the tree fell.</summary>
        public const string WoodLevel = "saga.wood_level";
        /// <summary>int on a log's ZDO: its domino depth; 0 for a tree felled by a swing, 1 for a tree a felled log knocked over.</summary>
        public const string WoodChain = "saga.wood_chain";
        /// <summary>bool on a log's ZDO: the log was split cleanly, so its wood gets the clean split bonus.</summary>
        public const string CleanSplit = "saga.clean_split";
        /// <summary>float 0..1 on a log's ZDO: how big its tree was within its kind's size range (old growth).</summary>
        public const string WoodSize = "saga.wood_size";
        /// <summary>Routed RPC from the server. ZPackage: long playerId, int kind, string species, float amount, int chain. A client applies it only when the sender is the server.</summary>
        public const string RpcWoodCredit = "saga.WoodCredit";
        /// <summary>Routed RPC to the server. The same fields as <see cref="RpcWoodCredit"/>, then the source ZDO id. The server checks ownership and the cap, then sends <see cref="RpcWoodCredit"/>.</summary>
        public const string RpcWoodReport = "saga.WoodReport";
        /// <summary>Routed RPC to everybody: (Vector3 position, string text); each client near the spot shows the text there.</summary>
        public const string RpcWoodCallout = "saga.WoodCallout";
        /// <summary>Player custom data: comma-separated species (log prefab names) of every tree the character has felled.</summary>
        public const string FelledTrees = "saga.felled";
        /// <summary>bool on a sapling's ZDO: Replanting planted it, so a felled log lying on it delays it instead of killing it.</summary>
        public const string Replanted = "saga.replanted";
        /// <summary>Charter sync key of the Finds YAML files (Saga.Finds*.yml).</summary>
        public const string FindsSync = "saga.finds";

        // Pickaxes. Nothing is stored on rocks: vein stars are computed from the world seed and the position.
        /// <summary>On a MineRock5, to its owner: (int area, ZDOID miner) - the miner's next hit on that chunk is a clean strike. Sent before the hit, kept in memory for 30 s.</summary>
        public const string RpcCleanStrike = "saga.CleanStrike";
        /// <summary>Routed RPC to everybody: (Vector3 position, string text); each client near the spot shows the text there, if its Pickaxes callouts are on.</summary>
        public const string RpcMineCallout = "saga.MineCallout";
        /// <summary>Player custom data: comma-separated identities of every ore deposit the character has hit: its name token ("$piece_deposit_copper"), else its kind (prefab name without "_frac").</summary>
        public const string MinedDeposits = "saga.mined";
        /// <summary>Charter sync key of the Mine Finds YAML files (Saga.MineFinds*.yml).</summary>
        public const string MineFindsSync = "saga.mine_finds";

        // Foraging.
        /// <summary>The Foraging skill's identity: its SkillType number is this name's stable hash.</summary>
        public const string ForagingSkillName = "meadworks_foraging";
        /// <summary>float on a player's own ZDO, written by that player's client: their Foraging level (CustomSkills publishes it).</summary>
        public const string ForagingLevel = "saga.foraging_level";
        /// <summary>On a forage pickable, to its owner: (float level) - the sender's next pick of it rolls stars at this effective level. Sent just before the game's RPC_Pick, kept in memory for 10 s.</summary>
        public const string RpcForageMark = "saga.ForageMark";
        /// <summary>Player custom data: comma-separated item prefab names of every kind of forage the character has picked.</summary>
        public const string ForagedKinds = "saga.foraged";
        /// <summary>Charter sync key of the Forage YAML files (Saga.Forage*.yml).</summary>
        public const string ForageSync = "saga.forage";

        // Husbandry. Creature keys are written by the creature's owner.
        /// <summary>The Husbandry skill's identity: its SkillType number is this name's stable hash.</summary>
        public const string HusbandrySkillName = "saga.husbandry";
        /// <summary>float on a player's own ZDO, written by that player's client: their Husbandry level (CustomSkills publishes it).</summary>
        public const string HusbandryLevel = "saga.husbandry_level";
        /// <summary>long on a creature being tamed: player ID of the last player who hurt it; that player's calm is broken.</summary>
        public const string CalmBrokenBy = "saga.calm_broken_by";
        /// <summary>long on a creature being tamed: world time (ticks) when <see cref="CalmBrokenBy"/> last hurt it.</summary>
        public const string CalmBrokenAt = "saga.calm_broken_at";
        /// <summary>int on a breeding creature: how many births it has had, so nearby keepers' clients can see each one.</summary>
        public const string Births = "saga.births";
        /// <summary>bool on a breeding creature: its current pregnancy is a twin's, so it cannot roll twins again.</summary>
        public const string Twin = "saga.twin";
        /// <summary>int on a creature about to give birth: 1 when the newborn gets one star more. Elite Creatures Reborn reads it.</summary>
        public const string StarUp = "saga.star_up";
        /// <summary>long on a tamed creature: world time (ticks) until which it is content from petting.</summary>
        public const string ContentUntil = "saga.content_until";
        /// <summary>long on a tamed creature: world time (ticks) of its last produce roll.</summary>
        public const string ProduceLast = "saga.produce_last";
        /// <summary>int on a tamed creature's ragdoll: the stars its meat drops carry (Prime Cuts), written by the creature's owner.</summary>
        public const string PrimeStars = "saga.prime_stars";
        /// <summary>On a tamed creature, to its owner: () - a player petted it; it becomes content.</summary>
        public const string RpcPet = "saga.Pet";
        /// <summary>On an animal feeder, to its owner: (string itemName) - a creature ate one of that item from it.</summary>
        public const string RpcFeederTake = "saga.FeederTake";
        /// <summary>Routed RPC to everybody: (long playerId, int kind, string creaturePrefab); only that player's client acts on it.</summary>
        public const string RpcHusbandryCredit = "saga.HusbandryCredit";
        /// <summary>Routed RPC to everybody: (Vector3 position, string text); each client near the spot shows it, if its Husbandry callouts are on.</summary>
        public const string RpcHerdCallout = "saga.HerdCallout";
        /// <summary>Player custom data: comma-separated prefab names of every kind of creature the character has tamed.</summary>
        public const string TamedKinds = "saga.tamed";
        /// <summary>The animal feeder piece's prefab name, cloned from the game's barrel.</summary>
        public const string FeederPrefab = "saga.feeder";

        // Defense.
        /// <summary>The Defense skill's identity: its SkillType number is this name's stable hash.</summary>
        public const string DefenseSkillName = "meadworks_defense";
        /// <summary>float on a player's own ZDO, written by that player's client: their Defense level (Shield Wall reads a blocker's).</summary>
        public const string DefenseLevel = "saga.defense_level";
        /// <summary>Player custom data: comma-separated name tokens ("$enemy_greydwarf") of every kind of creature the character has blocked.</summary>
        public const string BlockedFoes = "saga.blocked";

        // Fishing. Float keys are written by the angler's client when the float lands (it owns the float).
        /// <summary>float on a fishing float's ZDO: the angler's Fishing level at the cast. Fish owners read it for bites.</summary>
        public const string AnglerLevel = "saga.angler_level";
        /// <summary>int on a fishing float's ZDO: the stars of the bait on the hook (0 for bait without stars).</summary>
        public const string BaitStars = "saga.bait_stars";
        /// <summary>long on a chum item's ZDO: world time (ticks) it was first seen floating, written by its owner.</summary>
        public const string ChumSince = "saga.chum_since";
        /// <summary>Routed RPC to everybody: (Vector3 position, string text); each client near the spot shows the text there, if its Fishing callouts are on.</summary>
        public const string RpcFishCallout = "saga.FishCallout";
        /// <summary>Routed RPC to everybody: (string text); every client shows it top left (a legendary catch).</summary>
        public const string RpcFishAnnounce = "saga.FishAnnounce";
        /// <summary>Player custom data: the angler's log, comma-separated "prefab=mask", bit n-1 set for each level n landed.</summary>
        public const string FishLog = "saga.fish_log";
        /// <summary>Player custom data: personal records, comma-separated "prefab=weight:level" (weight in kg, invariant culture).</summary>
        public const string FishRecords = "saga.fish_records";
        /// <summary>Charter sync key of the Snags YAML files (Saga.Snags*.yml).</summary>
        public const string SnagsSync = "saga.snags";

        // Farming. Plant keys are written by the planting client, which owns the new plant's ZDO.
        /// <summary>long on a plant's ZDO: player ID of the player who planted it (0 or absent: not planted by a player with Farming).</summary>
        public const string FarmPlanter = "saga.farm_planter";
        /// <summary>float on a plant's ZDO: the planter's Farming level when it was planted.</summary>
        public const string FarmLevel = "saga.farm_level";
        /// <summary>int on a plant's ZDO: the stars of the seed it was planted from (heirloom).</summary>
        public const string FarmSeedStars = "saga.farm_seed";
        /// <summary>bool on a plant's ZDO: a compost bin fertilized it.</summary>
        public const string FarmFed = "saga.farm_fed";
        /// <summary>int on a plant's ZDO: the in-game day it was last tended (EnvMan.GetDay).</summary>
        public const string FarmTended = "saga.farm_tended";
        /// <summary>int on a ripe crop's ZDO, written by the plant's owner when it grew: its stars.</summary>
        public const string FarmStars = "saga.farm_stars";
        /// <summary>bool on a ripe crop's ZDO: it grew into a giant crop.</summary>
        public const string FarmGiant = "saga.farm_giant";
        /// <summary>int on a ripe crop's ZDO: the prefab hash of the plant it grew from, for auto-replant and seed return.</summary>
        public const string FarmFrom = "saga.farm_from";
        /// <summary>On a plant, to its owner: (int day) - a player tended it on that in-game day.</summary>
        public const string RpcTend = "saga.Tend";
        /// <summary>On a plant, to its owner: () - a compost bin fertilized it.</summary>
        public const string RpcFertilize = "saga.Fertilize";
        /// <summary>Player custom data: comma-separated pickable prefab names of every crop kind the character has picked.</summary>
        public const string HarvestedCrops = "saga.harvested";

        // Windmill: the stars of the queued items, parallel to the game's item0..itemN queue.
        /// <summary>string on a mill's ZDO: one digit per queued item, aligned to the end of the queue (older items without a digit have 0 stars).</summary>
        public const string MillStars = "saga.mill_stars";
        /// <summary>int on a mill's ZDO: the stars of the output stack it is gathering (s_spawnOre / s_spawnAmount).</summary>
        public const string MillSpawnStars = "saga.mill_spawn";
        /// <summary>On a Smelter, to its owner. ZPackage: string item, bool cheated, int stars.</summary>
        public const string RpcAddMill = "saga.AddMill";

        // Compost bin.
        /// <summary>The compost bin's prefab name (a copy of the game's barrel), the same on every machine.</summary>
        public const string CompostPrefab = "saga.compost_bin";
        /// <summary>float on a compost bin's ZDO: its compost points.</summary>
        public const string CompostPoints = "saga.compost";
        /// <summary>On a compost bin, to its owner: (float points) - kitchen trash to add.</summary>
        public const string RpcAddCompost = "saga.AddCompost";
    }
}

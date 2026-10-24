// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names, defaults and ranges follow design section 4; the EliteCreaturesReborn,
// CreatureManager and SaveYourLuckBruh options they replace are recorded in the design's Source column (names only).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Loot;
using ExtraPotions.Wildblood.Stars;
using ExtraPotions.Wildblood.Traits;
using static ExtraPotions.Wildblood.Config.SettingScope;

namespace ExtraPotions.Wildblood.Config
{
    /// <summary>
    /// Every Wildblood setting of design section 4 in file order (4.9, the rule file, is not a setting). The plugin binds
    /// exactly this list; the portable tests compare it with the spec tables. Deviation from design 4.12, recorded in the
    /// task document: the three drop multipliers are ServerAuthoritative numbers that act only while the Drop Multipliers
    /// cheat is allowed and on, so every Cheat-scope entry is a toggle whose safe value is off.
    /// </summary>
    public static class WildbloodSettings
    {
        public static class General
        {
            public const string Section = "1 - General";

            public static readonly WildbloodSetting<bool> Enabled = WildbloodSetting.Toggle(Section, "Enabled", true, ServerAuthoritative, "Whole product. Off: creatures, loot and luck behave as the game does.");
            public static readonly WildbloodSetting<bool> StarsEnabled = WildbloodSetting.Toggle(Section, "Stars Enabled", true, ServerAuthoritative, "Creature stars and difficulty. Off: creatures spawn with the game's levels.");
            public static readonly WildbloodSetting<bool> TraitsEnabled = WildbloodSetting.Toggle(Section, "Traits Enabled", true, ServerAuthoritative, "Mutations and modifiers on creatures. Off: no new traits roll.");
            public static readonly WildbloodSetting<bool> BossesEnabled = WildbloodSetting.Toggle(Section, "Bosses Enabled", true, ServerAuthoritative, "Boss aspects, boss stars, trophies, damage board and hints.");
            public static readonly WildbloodSetting<bool> LootEnabled = WildbloodSetting.Toggle(Section, "Loot Enabled", true, ServerAuthoritative, "Wildblood loot rules for starred creatures. Off: creatures drop the game's loot.");
            public static readonly WildbloodSetting<bool> LuckEnabled = WildbloodSetting.Toggle(Section, "Luck Enabled", true, ServerAuthoritative, "Bad-luck protection that survives restarts.");
            public static readonly WildbloodSetting<bool> PressureEnabled = WildbloodSetting.Toggle(Section, "Pressure Enabled", true, ServerAuthoritative, "Karma, Enforcers and multiplayer scaling.");
            public static readonly WildbloodSetting<string> ProtectedPrefabs = WildbloodSetting.Text(Section, "Protected Prefabs", "FrozenKing, FrozenKing_p2", ServerAuthoritative,
                "Comma list of prefabs that are never starred, traited or scaled. Creatures already rolled keep what they rolled.", NameList.Normalize);
        }

        public static class Stars
        {
            public const string Section = "2 - Stars";

            public static readonly WildbloodSetting<Difficulty> Difficulty = WildbloodSetting.Choice(Section, "Difficulty", ExtraPotions.Wildblood.Stars.Difficulty.Medium, World,
                "Star caps, star odds and mutation rates by biome and world tier: Easy, Medium, Hard, VeryHard, Extreme, or Custom (the rule file's biome rows).");
            public static readonly WildbloodSetting<StarMode> StarMode = WildbloodSetting.Choice(Section, "Star Mode", ExtraPotions.Wildblood.Stars.StarMode.Wildblood, World,
                "Wildblood rolls stars; KeepExisting keeps the level the game or another mod gave; Off rolls no stars.");
            public static readonly WildbloodSetting<int> MaxStars = WildbloodSetting.Integer(Section, "Max Stars", 8, 0, 8, ServerAuthoritative, "Most stars a creature can roll.");
            public static readonly WildbloodSetting<bool> DistanceScaling = WildbloodSetting.Toggle(Section, "Distance Scaling", false, ServerAuthoritative, "Health and damage (and modifier chances) rise with distance from the world center, by the distanceScaling bands in the rule file. Default band: +3% damage and +8% health per 1000 m, up to 5 steps.");
            public static readonly WildbloodSetting<bool> ScaleSaddleable = WildbloodSetting.Toggle(Section, "Scale Saddle-able Creatures", true, ServerAuthoritative, "Off: creatures that take a saddle keep their size at any star count.");
            public static readonly WildbloodSetting<bool> RespectEnemyLevelUpRate = WildbloodSetting.Toggle(Section, "Respect Enemy Level Up Rate", true, ServerAuthoritative, "The world modifier for enemy level-up rate scales star odds.");
        }

        public static class WorldTier
        {
            public const string Section = "3 - World Tier";

            public const string DefaultBossKeys = "defeated_eikthyr, defeated_gdking, defeated_bonemass, defeated_dragon, defeated_goblinking, defeated_queen, defeated_fader, defeated_frozenking_p3";

            public static readonly WildbloodSetting<bool> Enabled = WildbloodSetting.Toggle(Section, "Enabled", true, World, "The world hardens one tier each time a listed boss first falls.");
            public static readonly WildbloodSetting<string> BossKeys = WildbloodSetting.Text(Section, "Boss Keys", DefaultBossKeys, World, "Comma list of global keys that each add one tier.", NameList.Normalize);
            public static readonly WildbloodSetting<int> Override = WildbloodSetting.Integer(Section, "Override", -1, -1, 8, World, "Fixed world tier; -1 counts bosses.");
            public static readonly WildbloodSetting<bool> Announce = WildbloodSetting.Toggle(Section, "Announce", true, ServerAuthoritative, "Show \"The world hardens\" when the tier rises.");
        }

        public static class Traits
        {
            public const string Section = "4 - Traits";

            public static readonly WildbloodSetting<int> MaxMutations = WildbloodSetting.Integer(Section, "Max Mutations", 1, 0, 5, ServerAuthoritative, "Most mutations one creature carries; 0 means no limit.");
            public static readonly WildbloodSetting<ModifierCap> CreatureModifiers = WildbloodSetting.Choice(Section, "Creature Modifiers", ModifierCap.Max2, ServerAuthoritative, "Most modifiers a creature carries, at most one per group. Max1 to Max4: up to that many; Off: none.");
            public static readonly WildbloodSetting<float> LargeStarPower = WildbloodSetting.Number(Section, "Large Star Power", 1f, 0f, 5f, ServerAuthoritative, "Multiplier of E-marked mutation numbers on creatures with five or more stars.");
            public static readonly WildbloodSetting<float> BlinkAlertGrace = WildbloodSetting.Number(Section, "Blink Alert Grace", 3f, 0f, 10f, ServerAuthoritative, "Seconds after a Blinking creature is alerted before it may blink.");
            public static readonly WildbloodSetting<int> ReflectionDamageCap = WildbloodSetting.Integer(Section, "Reflection Damage Cap", 25, 0, 100000, ServerAuthoritative, "Most health an attacker loses per reflection; 0 means no cap.");
            public static readonly WildbloodSetting<bool> TamedKeepTraits = WildbloodSetting.Toggle(Section, "Tamed Keep Traits", true, ServerAuthoritative, "Taming keeps a creature's traits (tamed variants apply). Off: while a creature is tamed its mutations and modifiers do not act; they stay stored, and its stars stay. Young born while this is off inherit no mutation.");
            public static readonly WildbloodSetting<bool> TamedStars = WildbloodSetting.Toggle(Section, "Tamed Stars", true, ServerAuthoritative, "On: a new birth may inherit stars, and Shepherd follows this when it is installed. While Stars Enabled is on and Star Mode is Wildblood, a tamed creature with a stored star roll that was left at the game's level regains those stars at once if it is loaded, or the next time it loads. Its health stays the same share of the health bar (a full bar stays full, and an empty bar stays empty). Its size and color match the stars. The restore does not change its mutations. Off: a newborn keeps its parent's level, as the game does, and no bonus star is added. Turning this off does not remove stars a tame already has, including stars it just regained. Taming never removes stars, health, or mutations. Bosses, fresh births without a saved parked level or restore mark, and creatures with no stored star roll stay as they are. Parked births can regain stored stars too. Without a restore mark, a tame must still be at its saved parked level. The server sets this.");
            public static readonly WildbloodSetting<bool> PlayersMayInspect = WildbloodSetting.Toggle(Section, "Players May Inspect", true, ServerAuthoritative, "Host rule: every player may run wildblood inspect, which lists the stars, traits and resulting values of the creature under the crosshair. It only reads, so it is not a cheat. Off: admins only.");
        }

        public static class Bosses
        {
            public const string Section = "5 - Bosses";

            public static readonly WildbloodSetting<bool> BossStars = WildbloodSetting.Toggle(Section, "Boss Stars", true, ServerAuthoritative, "Bosses roll stars on their own table.");
            public static readonly WildbloodSetting<bool> Aspects = WildbloodSetting.Toggle(Section, "Aspects", true, ServerAuthoritative, "Bosses take an aspect at their altar or on appearance.");
            public static readonly WildbloodSetting<int> AltarShiftSeconds = WildbloodSetting.Integer(Section, "Altar Shift Seconds", 15, 0, 600, ServerAuthoritative, "Seconds between altar aspect shifts; 0 never shifts.");
            public static readonly WildbloodSetting<ModifierCap> BossModifiers = WildbloodSetting.Choice(Section, "Boss Modifiers", ModifierCap.Off, ServerAuthoritative, "Most modifiers a boss carries. Max1 to Max4: up to that many; Off (default): none, so bosses have only their aspects.");
            public static readonly WildbloodSetting<bool> DamageBoard = WildbloodSetting.Toggle(Section, "Damage Board", true, ServerAuthoritative, "Show each player's damage when a boss dies.");
            public static readonly WildbloodSetting<bool> BossHints = WildbloodSetting.Toggle(Section, "Boss Hints", true, ServerAuthoritative, "Server rule: allows hints that point players near a fallen boss toward the next point of interest. Players can still hide them with Display / Boss Hints.");
            public static readonly WildbloodSetting<bool> BlockSpawnsNearBoss = WildbloodSetting.Toggle(Section, "Block Spawns Near Boss", true, ServerAuthoritative, "No natural spawns near an active boss.");
            public static readonly WildbloodSetting<bool> TamedPressure = WildbloodSetting.Toggle(Section, "Tamed Pressure", false, ServerAuthoritative, "Host or server: every 5 seconds, select up to 4 nearest living tamed MonsterAI creatures within 32 horizontal meters of each boss. Generic damage equal to 1% of max health is scheduled each second. Tiny amounts accumulate into pulses; normal damage scaling still applies. Each receives 25% more damage and deals 25% less damage. This can kill tames. Effects can last up to 5.5 seconds after leaving range or a boss dying. Off stops effects immediately. Off by default; steps aside while BossRules is installed. Stars, max health and traits are unchanged. Discovery is incremental, so newly spawned or unloaded bosses can take longer to be found in large or changing worlds. Known bosses refresh every 5 seconds; local selection cost depends on nearby creature density.");
            public static readonly WildbloodSetting<bool> AbandonedBosses = WildbloodSetting.Toggle(Section, "Abandoned Bosses", false, ServerAuthoritative, "Host or server: remove a recorded summoned boss after Abandon Minutes at full health with no player inside Abandon Range, and return its offering at the altar once. Off by default. A normal kill gives no refund.");
            public static readonly WildbloodSetting<int> AbandonMinutes = WildbloodSetting.Integer(Section, "Abandon Minutes", 10, 1, 1440, ServerAuthoritative, "Host or server: continuous minutes a recorded summoned boss must remain at full health with no player inside Abandon Range before removal and one offering refund. A nearby player or missing health resets the timer. Used while Abandoned Bosses is on.");
            public static readonly WildbloodSetting<float> AbandonRange = WildbloodSetting.Number(Section, "Abandon Range", 100f, 10f, 1000f, ServerAuthoritative, "Host or server: distance from a recorded summoned boss in meters within which any player resets its abandonment timer. Used while Abandoned Bosses is on.");
        }

        public static class Loot
        {
            public const string Section = "6 - Loot";

            public static readonly WildbloodSetting<LootMode> Mode = WildbloodSetting.Choice(Section, "Mode", LootMode.Rolled, World,
                "Vanilla; Scaled (amounts times the star loot line); Rolled (table rolled again per star); Amount (base chance kept, amounts scale); Curated (only listed drops).");
            public static readonly WildbloodSetting<string> ExtraRollChance = WildbloodSetting.Text(Section, "Extra Roll Chance", "0, 100", ServerAuthoritative,
                "Rolled: percent chance of an extra roll for each star, by star; the last entry repeats.", t => PercentList.Normalize(t, "0, 100"));
            public static readonly WildbloodSetting<int> MaxExtraRolls = WildbloodSetting.Integer(Section, "Max Extra Rolls", 5, 0, 20, ServerAuthoritative, "Rolled: most extra rolls per kill; 0 means no limit.");
            public static readonly WildbloodSetting<int> AmountPerStar = WildbloodSetting.Integer(Section, "Amount Per Star", 50, 0, 100, ServerAuthoritative, "Amount: percent more of each successful drop per star.");
            public static readonly WildbloodSetting<int> AmountPerStarBosses = WildbloodSetting.Integer(Section, "Amount Per Star Bosses", 50, 0, 100, ServerAuthoritative, "Amount mode: percent more of each successful boss drop per star.");
            public static readonly WildbloodSetting<bool> MultiplyTrophies = WildbloodSetting.Toggle(Section, "Multiply Trophies", false, ServerAuthoritative, "Star rules also multiply trophies.");
            public static readonly WildbloodSetting<bool> DeferToDropNSpawn = WildbloodSetting.Toggle(Section, "Defer To DropNSpawn", true, ServerAuthoritative, "Loot steps aside while DropNSpawn is installed.");
            public static readonly WildbloodSetting<bool> SeasonalMeat = WildbloodSetting.Toggle(Section, "Seasonal Meat", true, ServerAuthoritative, "Host or server: while Solstice seasons are on, meat and hide from creatures use that season's published rate. Spring and summer drop less, fall and winter drop more, between one quarter and three times the usual amount. Below one, fractional amounts are rolled; a single item may not drop. Trophies, feathers, fangs, scales and coins stay as they are. Off, missing Solstice, or seasons turned off keeps today's drops. The next kill uses the current rate.");
            public static readonly WildbloodSetting<bool> SeasonalWood = WildbloodSetting.Toggle(Section, "Seasonal Wood", true, ServerAuthoritative, "Host or server: while Solstice seasons are on, wood from felled trees uses that season's published rate. Spring and summer drop less, fall and winter drop more, between one quarter and three times the usual amount. Below one, fractional amounts are rolled; a single item may not drop. Off, missing Solstice, or seasons turned off keeps today's wood. The next tree uses the current rate.");
        }

        public static class Luck
        {
            public const string Section = "7 - Luck";

            public static readonly WildbloodSetting<bool> PersistCounters = WildbloodSetting.Toggle(Section, "Persist Counters", true, ServerAuthoritative, "Bad-luck counters are saved with the character, per world.");
            public static readonly WildbloodSetting<bool> AttributeToKiller = WildbloodSetting.Toggle(Section, "Attribute To Killer", true, ServerAuthoritative, "Counters advance for the player who made the kill, whichever machine owns the creature.");
            public static readonly WildbloodSetting<float> TameCreditRange = WildbloodSetting.Number(Section, "Tame Credit Range", 30f, 5f, 100f, ServerAuthoritative, "Meters within which a tame's kill credits a player.");
        }

        public static class Pressure
        {
            public const string Section = "8 - Pressure";

            public static readonly WildbloodSetting<KarmaMode> KarmaMode = WildbloodSetting.Choice(Section, "Karma Mode", ExtraPotions.Wildblood.Config.KarmaMode.KarmaLevelAndEnforcer, ServerAuthoritative, "How Karma answers heavy killing in an area. KarmaLevelAndEnforcer: creatures there gain levels and Enforcers come; KarmaLevelOnly: levels only; EnforcerOnly: Enforcers only; Off: no Karma.");
            public static readonly WildbloodSetting<int> MaxEnforcersPerSector = WildbloodSetting.Integer(Section, "Max Enforcers Per Sector", 1, 1, 20, ServerAuthoritative, "Most active Enforcers in one 3 x 3 Karma neighborhood.");
            public static readonly WildbloodSetting<int> DungeonEnforcerDelay = WildbloodSetting.Integer(Section, "Dungeon Enforcer Delay", 7, 0, 30, ServerAuthoritative, "Seconds between the dungeon warning and the Enforcer.");
            public static readonly WildbloodSetting<int> EnforcerAbandonDespawn = WildbloodSetting.Integer(Section, "Enforcer Abandon Despawn", 120, 0, 1500, ServerAuthoritative, "Seconds without a player near before an Enforcer leaves; 0 never.");
            public static readonly WildbloodSetting<bool> BlockEnforcerWhileBossActive = WildbloodSetting.Toggle(Section, "Block Enforcer While Boss Active", true, ServerAuthoritative, "No Enforcer while a boss is active nearby.");
            public static readonly WildbloodSetting<bool> BlockKarmaWhileBossActive = WildbloodSetting.Toggle(Section, "Block Karma While Boss Active", true, ServerAuthoritative, "Kills add no Karma while a boss is alive nearby.");
            public static readonly WildbloodSetting<bool> BlockKarmaWhileEnforcerActive = WildbloodSetting.Toggle(Section, "Block Karma While Enforcer Active", true, ServerAuthoritative, "Kills add no Karma while an Enforcer is alive nearby.");
            public static readonly WildbloodSetting<bool> BlockOmenDuringCooldown = WildbloodSetting.Toggle(Section, "Block Omen During Cooldown", true, ServerAuthoritative, "Omen cannot summon an Enforcer during the region's cooldown.");
            public static readonly WildbloodSetting<bool> BlockSpawnsNearEnforcer = WildbloodSetting.Toggle(Section, "Block Spawns Near Enforcer", true, ServerAuthoritative, "No natural spawns near a living Enforcer.");
            public static readonly WildbloodSetting<float> HealthPerExtraPlayer = WildbloodSetting.Number(Section, "Health Per Extra Player", 30f, 0f, 200f, ServerAuthoritative, "Percent more creature health per nearby player after the first.");
            public static readonly WildbloodSetting<float> DamagePerExtraPlayer = WildbloodSetting.Number(Section, "Damage Per Extra Player", 4f, 0f, 200f, ServerAuthoritative, "Percent more creature damage per nearby player after the first.");
            public static readonly WildbloodSetting<int> MaxPlayersForScaling = WildbloodSetting.Integer(Section, "Max Players For Scaling", 5, 1, 25, ServerAuthoritative, "Most nearby players counted for scaling.");
            public static readonly WildbloodSetting<string> YieldScalingTo = WildbloodSetting.Text(Section, "Yield Scaling To", "", ServerAuthoritative,
                "Comma list of plugin GUIDs of other mods that set the game's multiplayer difficulty. While one is installed, the three per-player settings above leave the game's values alone. CreatureManager is always honored.", NameList.Normalize);
        }

        public static class Display
        {
            public const string Section = "10 - Display";

            public static readonly WildbloodSetting<bool> ShowTraitNames = WildbloodSetting.Toggle(Section, "Show Trait Names", true, Local, "Mutation words before creature names.");
            public static readonly WildbloodSetting<bool> TraitNamesAboveName = WildbloodSetting.Toggle(Section, "Trait Names Above Creature Name", true, Local,
                "Shows a creature's mutations, boss aspects and modifiers once each on their own smaller line above its health bar name, and the name itself without trait words. Applies to ordinary creature health bars; boss bars keep the trait words in the name. Needs Show Trait Names. Your personal client display setting; it never changes what other players see. Default: on.");
            public static readonly WildbloodSetting<bool> ColouredStars = WildbloodSetting.Toggle(Section, "Colored Stars", true, Local, "Stars take the mutation's color.");
            public static readonly WildbloodSetting<float> NameplateDistance = WildbloodSetting.Number(Section, "Nameplate Distance", 0f, 0f, 50f, Local, "Meters at which nameplates show; 0 keeps the game's.");
            public static readonly WildbloodSetting<IconLayout> ModifierIconLayout = WildbloodSetting.Choice(Section, "Modifier Icon Layout", IconLayout.FixedCategorySlots, Local, "How modifier icons sit above a creature. FixedCategorySlots: each category keeps its own place, leaving gaps; RightPacked: icons line up from the right with no gaps.");
            public static readonly WildbloodSetting<float> EffectDensity = WildbloodSetting.Number(Section, "Effect Density", 1f, 0f, 1f, Local, "Share of cosmetic trait effects drawn; warnings always show.");
            public static readonly WildbloodSetting<float> SmallStarSize = WildbloodSetting.Number(Section, "Small Star Size", 1.3f, 0.5f, 4f, Local, "Size of a small star above creature names, as a multiple of the game's star size (1 = same, 1.3 = 30% larger).");
            public static readonly WildbloodSetting<float> LargeStarSize = WildbloodSetting.Number(Section, "Large Star Size", 2.2f, 0.5f, 6f, Local, "Size of a large star (worth five stars), as a multiple of the game's star size.");
            public static readonly WildbloodSetting<bool> ShowStolenItems = WildbloodSetting.Toggle(Section, "Show Stolen Items", true, Local, "Icon of what a Thieving creature holds.");
            public static readonly WildbloodSetting<bool> ShowDevouredCreatures = WildbloodSetting.Toggle(Section, "Show Devoured Creatures", true, Local, "Icon of what a Devouring creature ate.");
            public static readonly WildbloodSetting<float> IconSize = WildbloodSetting.Number(Section, "Icon Size", 1.6f, 0.5f, 4f, Local, "Size of stolen and devoured icons.");
            public static readonly WildbloodSetting<bool> SneakInspect = WildbloodSetting.Toggle(Section, "Sneak Inspect", true, Local, "While crouching, looking at a creature shows its resistances and weaknesses.");
            public static readonly WildbloodSetting<int> DamageBoardSeconds = WildbloodSetting.Integer(Section, "Damage Board Seconds", 60, 5, 600, Local, "Seconds the boss damage board stays open.");
            public static readonly WildbloodSetting<bool> BossHints = WildbloodSetting.Toggle(Section, "Boss Hints", true, Local, "Your own display choice: show the boss hints to you. Works only while the server's Bosses / Boss Hints is on.");
            public static readonly WildbloodSetting<bool> ShowKarmaUnderMinimap = WildbloodSetting.Toggle(Section, "Show Karma Under Minimap", true, Local, "Karma line inside the minimap's bottom-right corner. The world tier for Mantle's square beside the minimap always shows.");
            public static readonly WildbloodSetting<string> Palette = WildbloodSetting.Text(Section, "Palette", MutationPalette.Default, Local, "Star color per mutation as Name=#RRGGBB, comma separated.", MutationPalette.Normalize);
            public static readonly WildbloodSetting<string> LoreWindowKey = WildbloodSetting.Text(Section, "Lore Window Key", "", Local, "Key that opens the Wildblood page of the Compendium. Empty (default): no key; the Meadworks menu (F8, Esc > Meadworks) lists the lore.", t => (t ?? "").Trim());
            public static readonly WildbloodSetting<string> LoreWindowPadChord = WildbloodSetting.Text(Section, "Lore Window Pad Chord", "", Local, "Gamepad chord that opens the Wildblood page of the Compendium. Empty: none.", t => (t ?? "").Trim());
        }

        public static class Recap
        {
            public const string Section = "11 - Recap";

            public static readonly WildbloodSetting<bool> RecordDeaths = WildbloodSetting.Toggle(Section, "Record Deaths", true, Local, "Keep a recap of my last deaths.");
            public static readonly WildbloodSetting<string> RecapKey = WildbloodSetting.Text(Section, "Recap Key", "", Local, "Key that opens the death recap. Empty (default): no key; the Meadworks menu (F8, Esc > Meadworks) lists the recap.", t => (t ?? "").Trim());
            public static readonly WildbloodSetting<bool> DeathNotice = WildbloodSetting.Toggle(Section, "Death Notice", true, Local, "After a death, say how to open the recap.");
            public static readonly WildbloodSetting<int> DeathsKept = WildbloodSetting.Integer(Section, "Deaths Kept", 5, 1, 10, Local, "Deaths kept in the recap.");
            public static readonly WildbloodSetting<int> SecondsBeforeDeath = WildbloodSetting.Integer(Section, "Seconds Before Death", 15, 5, 30, Local, "Seconds recorded before each death.");
            public static readonly WildbloodSetting<int> VideoFramesPerSecond = WildbloodSetting.Integer(Section, "Video Frames Per Second", 10, 5, 30, Local, "Recap video frame rate.");
            public static readonly WildbloodSetting<int> VideoHeight = WildbloodSetting.Integer(Section, "Video Height", 360, 180, 720, Local, "Recap video height in pixels.");
        }

        public static class Horde
        {
            public const string Section = "13 - Horde";
            public static readonly WildbloodSetting<bool> Enabled = WildbloodSetting.Toggle(Section, "Horde", false, ServerAuthoritative, "More naturally spawned creatures, each weaker. Bosses, events, dungeon/camp spawners and tame creatures are excluded.");
            public static readonly WildbloodSetting<float> SpawnMultiplier = WildbloodSetting.Number(Section, "Spawn Multiplier", 2f, 1f, 3f, ServerAuthoritative, "Natural spawn groups and maximum populations; their own loot scales inversely.");
            public static readonly WildbloodSetting<float> HealthMultiplier = WildbloodSetting.Number(Section, "Creature Health Multiplier", .65f, .1f, 2f, ServerAuthoritative, "Tagged natural creatures' health, composed with existing stars. Applied when they load.");
            public static readonly WildbloodSetting<float> AttackMultiplier = WildbloodSetting.Number(Section, "Creature Attack Multiplier", .75f, .1f, 2f, ServerAuthoritative, "Attack damage multiplier for naturally spawned creatures that Horde added. Changes apply to them right away.");
        }

        public static class AI
        {
            public const string Section="14 - AI";
            public static readonly WildbloodSetting<bool> Enabled=WildbloodSetting.Toggle(Section,"Player Target Switching",false,ServerAuthoritative,"When a creature looks for a target, it sometimes switches to another hostile player it can sense. Separate from stars and traits; the machine that controls the creature decides.");
            public static readonly WildbloodSetting<float> Interval=WildbloodSetting.Number(Section,"Player Target Switch Interval (s)",10f,1f,120f,ServerAuthoritative,"Minimum seconds between attempts, including failed or unreachable selections. Changes restart pending attempts.");
            public static readonly WildbloodSetting<int> Chance=WildbloodSetting.Integer(Section,"Player Target Switch Chance (%)",25,0,100,ServerAuthoritative,"Chance per attempt. Zero disables extra selection.");
            public static readonly WildbloodSetting<float> Hold=WildbloodSetting.Number(Section,"Player Target Hold Duration (s)",5f,0f,30f,ServerAuthoritative,"Seconds a creature keeps the player it switched to before its next target search may replace them. 0 disables holding. The game still drops targets it loses, and hunting or provoked creatures keep their own priorities.");
        }

        public static class Cheats
        {
            public const string Section = "12 - Cheats";

            public static readonly WildbloodSetting<bool> DropMultipliers = WildbloodSetting.Toggle(Section, "Drop Multipliers", false, Cheat,
                "Cheat. The three drop multipliers below act. Off whenever the server blocks cheats.", safe: false);
            public static readonly WildbloodSetting<float> CreatureDropMultiplier = WildbloodSetting.Number(Section, "Creature Drop Multiplier", 1f, 0f, 10f, ServerAuthoritative,
                "While Drop Multipliers is on: multiplies non-trophy creature drops after the loot mode.");
            public static readonly WildbloodSetting<float> BossDropMultiplier = WildbloodSetting.Number(Section, "Boss Drop Multiplier", 1f, 0f, 10f, ServerAuthoritative,
                "While Drop Multipliers is on: multiplies boss drops on top.");
            public static readonly WildbloodSetting<float> TrophyDropMultiplier = WildbloodSetting.Number(Section, "Trophy Drop Multiplier", 1f, 0f, 10f, ServerAuthoritative,
                "While Drop Multipliers is on: multiplies trophy amounts when a trophy drops.");
            public static readonly WildbloodSetting<bool> LuckyKills = WildbloodSetting.Toggle(Section, "Lucky Kills", false, Cheat,
                "Cheat. Every bad-luck-protected row drops on each kill; counters are untouched.", safe: false);
            public static readonly WildbloodSetting<bool> CreatureCommands = WildbloodSetting.Toggle(Section, "Creature Commands", false, Cheat,
                "Cheat. Enables the admin commands wildblood spawn, purge and karma.", safe: false);
        }

        // Deferred so that every nested holder is initialized first (Mono initializes the containing type eagerly).
        private static readonly Lazy<WildbloodSetting[]> all = new Lazy<WildbloodSetting[]>(() => new WildbloodSetting[]
        {
            General.Enabled, General.StarsEnabled, General.TraitsEnabled, General.BossesEnabled, General.LootEnabled, General.LuckEnabled,
            General.PressureEnabled, General.ProtectedPrefabs,
            Stars.Difficulty, Stars.StarMode, Stars.MaxStars, Stars.DistanceScaling, Stars.ScaleSaddleable, Stars.RespectEnemyLevelUpRate,
            WorldTier.Enabled, WorldTier.BossKeys, WorldTier.Override, WorldTier.Announce,
            Traits.MaxMutations, Traits.CreatureModifiers, Traits.LargeStarPower, Traits.BlinkAlertGrace, Traits.ReflectionDamageCap, Traits.TamedKeepTraits, Traits.TamedStars, Traits.PlayersMayInspect,
            Bosses.BossStars, Bosses.Aspects, Bosses.AltarShiftSeconds, Bosses.BossModifiers, Bosses.DamageBoard, Bosses.BossHints, Bosses.BlockSpawnsNearBoss,
            Bosses.TamedPressure, Bosses.AbandonedBosses, Bosses.AbandonMinutes, Bosses.AbandonRange,
            Loot.Mode, Loot.ExtraRollChance, Loot.MaxExtraRolls, Loot.AmountPerStar, Loot.AmountPerStarBosses, Loot.MultiplyTrophies, Loot.DeferToDropNSpawn, Loot.SeasonalMeat, Loot.SeasonalWood,
            Luck.PersistCounters, Luck.AttributeToKiller, Luck.TameCreditRange,
            Pressure.KarmaMode, Pressure.MaxEnforcersPerSector, Pressure.DungeonEnforcerDelay, Pressure.EnforcerAbandonDespawn,
            Pressure.BlockEnforcerWhileBossActive, Pressure.BlockKarmaWhileBossActive, Pressure.BlockKarmaWhileEnforcerActive,
            Pressure.BlockOmenDuringCooldown, Pressure.BlockSpawnsNearEnforcer, Pressure.HealthPerExtraPlayer, Pressure.DamagePerExtraPlayer,
            Pressure.MaxPlayersForScaling, Pressure.YieldScalingTo,
            Display.ShowTraitNames, Display.TraitNamesAboveName, Display.ColouredStars, Display.NameplateDistance, Display.ModifierIconLayout, Display.EffectDensity,
            Display.SmallStarSize, Display.LargeStarSize, Display.ShowStolenItems, Display.ShowDevouredCreatures, Display.IconSize,
            Display.SneakInspect, Display.DamageBoardSeconds, Display.BossHints, Display.ShowKarmaUnderMinimap,
            Display.Palette, Display.LoreWindowKey, Display.LoreWindowPadChord,
            Recap.RecordDeaths, Recap.RecapKey, Recap.DeathNotice, Recap.DeathsKept, Recap.SecondsBeforeDeath, Recap.VideoFramesPerSecond, Recap.VideoHeight,
            Cheats.DropMultipliers, Cheats.CreatureDropMultiplier, Cheats.BossDropMultiplier, Cheats.TrophyDropMultiplier, Cheats.LuckyKills, Cheats.CreatureCommands,
            Horde.Enabled, Horde.SpawnMultiplier, Horde.HealthMultiplier, Horde.AttackMultiplier,
            AI.Enabled, AI.Interval, AI.Chance, AI.Hold,
        });

        private static readonly Lazy<Dictionary<string, WildbloodSetting>> byId = new Lazy<Dictionary<string, WildbloodSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every setting in file order.</summary>
        public static IReadOnlyList<WildbloodSetting> All => all.Value;

        /// <summary>A setting by <c>Section.Key</c> in any case; null when unknown.</summary>
        public static WildbloodSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;
    }
}

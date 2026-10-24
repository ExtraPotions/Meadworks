// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Shepherd.Breeding;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    public enum ContainerSelection { AllExceptDenied, MarkedOnly }
    public enum LoreDetail { Compact, Full }

    /// <summary>Shepherd additions to the directly adopted native Husbandry settings (design section 4).</summary>
    public static class ShepherdSettings
    {
        public static HusbandrySetting<bool> Enabled = null!, FeedingEnabled = null!, BreedingEnabled = null!, HerdEnabled = null!;
        public static HusbandrySetting<bool> RequireMove = null!, FeedWild = null!, ContainerFeeding = null!, ProtectContainers = null!, ShowFood = null!;
        public static HusbandrySetting<float> MoveProximity = null!, GiveUp = null!, ContainerRange = null!, ReachThroughWalls = null!;
        public static HusbandrySetting<ContainerSelection> Selection = null!;
        public static HusbandrySetting<string> DisallowFeed = null!, DisallowAnimals = null!, PlankRecipe = null!, StoneRecipe = null!;
        public static HusbandrySetting<LoreDetail> Detail = null!;
        public static HusbandrySetting<InheritanceMode> Inheritance = null!;
        public static HusbandrySetting<bool> TamedStars = null!;
        public static HusbandrySetting<int> MaxAboveBestParent = null!, LandPenCapacity = null!;
        public static HusbandrySetting<string> LandPenRecipe = null!;
        public static HusbandrySetting<float> BirthStarChance = null!, TwinDelay = null!, MutationChance = null!;
        public static HusbandrySetting<bool> RecordLineage = null!, SeasonalBreeding = null!, StrictHerd = null!, PenMode = null!, PauseMovingShips = null!;
        public static HusbandrySetting<KeyboardShortcut> BookKey = null!;
        public static HusbandrySetting<string> BookPadChord = null!;
        public static HusbandrySetting<float> BookRange = null!;
        public static HusbandrySetting<bool> InstantTame = null!, InstantGrowth = null!, NoHunger = null!, UnlimitedHerd = null!, BreedingCommands = null!;

        public static void Initialize(NativeConfiguration config)
        {
            Enabled = config.Bind("1 - General", "Enabled", true, "Enables Shepherd. Saved Husbandry levels and lineage are kept when off.");
            FeedingEnabled = config.Bind("1 - General", "Feeding Enabled", true, "Enables trough and optional container feeding.");
            BreedingEnabled = config.Bind("1 - General", "Breeding Enabled", true, "Enables inheritance, twins, breeding pace and lineage.");
            HerdEnabled = config.Bind("1 - General", "Herd Enabled", true, "Enables Shepherd herd limits and pen policy.");
            RequireMove = config.Bind("2 - Feeding", "Require Move To Feed", true, "Animals must approach their food source before eating.");
            MoveProximity = config.Bind("2 - Feeding", "Move Proximity", 2.5f, "Maximum distance in meters from the source when eating.", acceptableValues: new AcceptableValueRange<float>(1f, 5f));
            ReachThroughWalls = config.Bind("2 - Feeding", "Reach Through Walls", 1f, "Maximum combined solid wall thickness in meters when approaching food. 0 requires a clear feeding reach. Floors and terrain block feeding.", acceptableValues: new AcceptableValueRange<float>(0f, 2f));
            GiveUp = config.Bind("2 - Feeding", "Give Up Seconds", 20f, "Seconds before abandoning an unreachable food source.", acceptableValues: new AcceptableValueRange<float>(5f, 120f));
            FeedWild = config.Bind("2 - Feeding", "Feed Wild Animals", false, "Untamed tameable animals may eat from troughs to begin taming.");
            ContainerFeeding = config.Bind("2 - Feeding", "Container Feeding", false, "Allows non-private player containers as food sources. Off by default.");
            ContainerRange = config.Bind("2 - Feeding", "Container Range", 10f, "Container search range in meters.", acceptableValues: new AcceptableValueRange<float>(2f, 50f));
            Selection = config.Bind("2 - Feeding", "Container Selection", ContainerSelection.AllExceptDenied, "Use eligible containers except denied ones, or only containers marked as a feed source.");
            DisallowFeed = config.Bind("2 - Feeding", "Disallow Feed", "", "Comma-separated item prefab names never fed automatically.");
            DisallowAnimals = config.Bind("2 - Feeding", "Disallow Animals", "", "Comma-separated creature prefab names never fed automatically.");
            ProtectContainers = config.Bind("2 - Feeding", "Protect Feed Containers", true, "Animals cannot damage their eligible troughs and feed containers.");
            ShowFood = config.Bind("2 - Feeding", "Show Food On Troughs", true, "Display stored food on trough straw.", synced: false);
            PlankRecipe = config.Bind("3 - Troughs", "Plank Trough Recipe", "Wood:6, Resin:2", "Comma-separated item:amount recipe. Unusable recipes fall back to Wood:10.");
            StoneRecipe = config.Bind("3 - Troughs", "Stone Trough Recipe", "Stone:12, Wood:2", "Comma-separated item:amount recipe. Unusable recipes fall back to Wood:10.");
            Detail = config.Bind("4 - Husbandry", "Lore Detail", LoreDetail.Full, "Compact gives one lore line; Full gives every available detail.", synced: false);
            Inheritance = config.Bind("6 - Breeding", "Star Inheritance", InheritanceMode.ParentMean, "How a newborn's base stars are chosen when Wildblood is installed and its Tamed Stars is on. Vanilla: the mother's stars, as in the game. BestParentUniform: a random number from 0 to the stronger parent's stars. ParentMean: the parents' average, rounded up. Without Wildblood, or when that setting is off, a newborn keeps its mother's level, as the game does.");
            TamedStars = config.BindHidden("6 - Breeding", "Tamed Stars", true, "Ignored. Wildblood decides creature stars. Without Wildblood, a newborn keeps its mother's level, as the game does. Kept so an older config file still loads.", synced: false);
            MaxAboveBestParent = config.Bind("6 - Breeding", "Max Above Best Parent", 1, "Maximum bonus levels above the stronger parent.", acceptableValues: new AcceptableValueRange<int>(0, 2));
            BirthStarChance = config.Bind("6 - Breeding", "Birth Star Chance", 0f, "Flat percent chance of an additional birth star, independent of Husbandry. It adds nothing without Wildblood, or when Wildblood's Tamed Stars is off.", acceptableValues: Settings.UpTo(100f));
            TwinDelay = config.Bind("6 - Breeding", "Twin Delay", 30f, "Seconds between the first birth and a twin.", acceptableValues: new AcceptableValueRange<float>(5f, 300f));
            MutationChance = config.Bind("6 - Breeding", "Mutation Chance", 100f, "Percent chance to carry a Wildblood mutation. Used only when Wildblood supplies traits.", acceptableValues: Settings.UpTo(100f));
            RecordLineage = config.Bind("6 - Breeding", "Record Lineage", true, "Record parent identities, generation, birth day and breeder.");
            SeasonalBreeding = config.Bind("6 - Breeding", "Seasonal Breeding", true, "Host or server: while Solstice seasons are on, tame breeding uses that season's published rate. Spring is faster, winter is slower, and a rate of zero starts no new pregnancies. The pace stays between one quarter and three times the usual pace. A pregnancy already underway still finishes. Off, missing Solstice, or seasons turned off keeps today's breeding. Feeding and troughs stay as they are. The next breeding check uses the current rate.");
            StrictHerd = config.Bind("7 - Herd", "Strict Herd Limit", false, "Delay a due birth while the herd is over its current limit.");
            PenMode = config.Bind("7 - Herd", "Pen Mode", false, "Count crowding within an assigned livestock pen. A pen capacity of 0 keeps the game's own crowding limit.");
            PauseMovingShips = config.Bind("7 - Herd", "Pause Breeding On Moving Ships", true, "Pause breeding in a moving Keel ship pen.");
            LandPenCapacity = config.Bind("7 - Herd", "Land Pen Capacity", 12, "With Pen Mode on, the most animals a land pen holds before breeding stops. 0 keeps the game's own crowding limit.", acceptableValues: new AcceptableValueRange<int>(0, 100));
            LandPenRecipe = config.Bind("7 - Herd", "Land Pen Recipe", "Wood:30, Resin:4", "Comma-separated item:amount recipe for the land pen.");
            BookKey = config.Bind("11 - Interface", "Herd Book Key", new KeyboardShortcut(), "Keyboard chord to open the herd book; unbound by default.", synced: false);
            BookPadChord = config.Bind("11 - Interface", "Herd Book Pad Chord", "", "Gamepad chord to open the herd book; unbound by default.", synced: false);
            BookRange = config.Bind("11 - Interface", "Herd Book Range", 50f, "Herd book search range in meters.", synced: false, acceptableValues: new AcceptableValueRange<float>(10f, 100f));
            InstantTame = config.Cheat("Instant Tame", "Complete taming after the first meal.");
            InstantGrowth = config.Cheat("Instant Growth", "Young grow and warm eggs hatch on the next update.");
            NoHunger = config.Cheat("No Hunger", "Tamed animals never become hungry.");
            UnlimitedHerd = config.Cheat("Unlimited Herd", "Crowding never stops breeding.");
            BreedingCommands = config.Cheat("Breeding Commands", "Enable shepherd breed and shepherd stars console commands.");
        }

        private static bool notedIgnoredStars;

        /// <summary>Once per process. The config key is kept and is not read.</summary>
        public static void NoteIgnoredStars(MeadworksLog log)
        {
            if (notedIgnoredStars || log == null) return;
            notedIgnoredStars = true;
            log.Info("Shepherd Tamed Stars is ignored. Wildblood decides creature stars. Without Wildblood, births keep the game's level.");
        }
    }
}

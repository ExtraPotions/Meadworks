// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names and ranges follow design section 4; the BetterSleepBruh and DoorOpenerBruh names
// they mirror are recorded in the design's Source column (names only, no code).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Ember.Doors;
using ExtraPotions.Ember.Sleep;
using ExtraPotions.Ember.Torches;
using static ExtraPotions.Ember.Config.SettingScope;

namespace ExtraPotions.Ember.Config
{
    /// <summary>The four settings of one door kind section (design 4.3); Check for Key only where the kind has it.</summary>
    public sealed class DoorKindSettings
    {
        internal DoorKindSettings(DoorKind kind)
        {
            Kind = kind;
            var section = EmberSettings.DoorSectionPrefix + kind.Section;
            Mechanic = EmberSetting.Choice(section, "Automation Mechanic", kind.DefaultMechanic, Local,
                (kind.GameName == null ? "" : "In game: " + kind.GameName + ". ")
                + "When doors of this kind open by themselves: OpenAllDoors, OnlyOpenSelfmadeDoors (built by you), OnlyOpenAllPlayerMadeDoors (built by any player) or DoNotOpenAutomatically.");
            OpenDistance = EmberSetting.Number(section, "Open Distance", kind.DefaultOpenDistance, DoorDistances.Min, DoorDistances.Max, Local,
                "Meters from the door at which it opens.");
            CloseDistance = EmberSetting.Number(section, "Close Distance", kind.DefaultCloseDistance, DoorDistances.Min, DoorDistances.Max, Local,
                "Meters from the door beyond which it closes again. Raised to Open Distance when set lower.");
            if (kind.HasCheckForKey)
                CheckForKey = EmberSetting.Toggle(section, "Check for Key", kind.DefaultCheckForKey, Local,
                    "Doors of this kind that need a key open by themselves only when this is on and you carry the key.");
        }

        public DoorKind Kind { get; }
        public EmberSetting<AutomationMechanic> Mechanic { get; }
        public EmberSetting<float> OpenDistance { get; }
        public EmberSetting<float> CloseDistance { get; }

        /// <summary>Null for kinds without the option (doors with a key item then never open by themselves).</summary>
        public EmberSetting<bool>? CheckForKey { get; }

        internal IEnumerable<EmberSetting> Entries()
        {
            yield return Mechanic;
            yield return OpenDistance;
            yield return CloseDistance;
            if (CheckForKey != null) yield return CheckForKey;
        }
    }

    /// <summary>
    /// Every Ember setting from design section 4, in file order. The plugin binds exactly this list; the portable tests
    /// compare it with the spec tables. Door kind sections are named <c>Doors: &lt;DoorOpenerBruh section&gt;</c>.
    /// </summary>
    public static class EmberSettings
    {
        public const string DoorSectionPrefix = "Doors: ";

        public static class Sleep
        {
            public const string Section = "Sleep";

            public static readonly EmberSetting<bool> Enabled = EmberSetting.Toggle(Section, "Enabled", true, ServerAuthoritative,
                "Ember's multiplayer sleep. Off: the game's own sleep and no sleep HUD.");
            public static readonly EmberSetting<bool> UseVanillaSleepWindow = EmberSetting.Toggle(Section, "Use Vanilla Sleep Window", false, ServerAuthoritative,
                "On: the game's sleep window and skip rule, and no partial sleep boost.");
            public static readonly EmberSetting<float> SleepStart = EmberSetting.Number(Section, "Sleep Start", SleepWindow.DefaultSleepStart,
                SleepWindow.MinSleepStart, SleepWindow.MaxSleepStart, ServerAuthoritative,
                "Day fraction from which beds can be used (0.5 is noon). Sleep ends at morning (0.25).");
            public static readonly EmberSetting<bool> PartialSleepBoost = EmberSetting.Toggle(Section, "Partial Sleep Boost", true, ServerAuthoritative,
                "While some but not all players are in bed, the night passes faster. Off: only the all-in-bed skip.");
            public static readonly EmberSetting<float> BonusMultiplier = EmberSetting.Number(Section, "Bonus Multiplier", BoostSettings.DefaultBonusMultiplier,
                0f, BoostSettings.MaxBonusMultiplier, ServerAuthoritative,
                "Extra rate = Bonus Multiplier x sleeping / (players - 1) x Bonus Increment Scale. Defaults give 12 (13x speed) with all but one in bed.");
            public static readonly EmberSetting<float> BonusIncrementScale = EmberSetting.Number(Section, "Bonus Increment Scale", BoostSettings.DefaultBonusIncrementScale,
                0f, BoostSettings.MaxBonusIncrementScale, ServerAuthoritative,
                "Scales how much each sleeping player speeds up the night while others stay awake. Multiplied with Bonus Multiplier; 0 turns the partial-sleep speedup off.");
            public static readonly EmberSetting<float> BoostFadeSeconds = EmberSetting.Number(Section, "Boost Fade Seconds", 3f, 0f, (float)SleepBoost.MaxFadeRealSeconds,
                ServerAuthoritative, "Real seconds before morning over which the boost ramps down to none. 0: hard stop at morning.");
            public static readonly EmberSetting<bool> WakeAtWindowEnd = EmberSetting.Toggle(Section, "Wake At Window End", true, ServerAuthoritative,
                "When morning comes without a full skip, everybody in bed wakes up.");
            public static readonly EmberSetting<bool> SleepInAnyBed = EmberSetting.Toggle(Section, "Sleep In Any Bed", true, ServerAuthoritative,
                "Sleep in a bed another player has claimed. The bed keeps its owner and your spawn point does not change; the game's time, roof, fire, wet and enemy checks still apply. The host decides; on by default.");
            public static readonly EmberSetting<bool> StandUpOnRespawn = EmberSetting.Toggle(Section, "Stand Up On Respawn", false, ServerAuthoritative,
                "After death, skip the getting-up animation when you respawn. Logging in and first-spawn flight keep the game's behavior. Requires Ember's Sleep module; off by default.");
            public static readonly EmberSetting<bool> NearestOwnedBedRespawn = EmberSetting.Toggle(Section, "Nearest Owned Bed Respawn", false, ServerAuthoritative,
                "After death, respawn at your nearest known owned bed. With at least two candidates and maps allowed, choose a bed on the native map for up to 30 seconds; timeout keeps the nearest. Map, Escape, or gamepad B confirms. Destroyed or reclaimed beds fall back to another known bed, then world start. Requires Sleep Enabled. Off by default; normal login is unchanged.");
            public static readonly EmberSetting<bool> QuickRespawn = EmberSetting.Toggle(Section, "Quick Respawn", false, ServerAuthoritative,
                "Shorten death and bed-loading waits near your respawn destination, up to the game's normal wait at 1,000 meters. Confirming a map bed choice skips the remaining waits; native area, bed and remote arrival checks still apply. With this option off, confirmation keeps the remaining native death delay and full loading wait. Requires Sleep Enabled; also works without nearest-bed selection. Disabling preserves pending requests and stops later acceleration. Off by default.");
            public static readonly EmberSetting<bool> OwnedBedMapPins = EmberSetting.Toggle(Section, "Show Owned Beds on Map", true, Local,
                "Requires Nearest Owned Bed Respawn and Sleep Enabled. Show learned owned beds for your character in this world as temporary yellow icons on the minimap and large map. The native bound-bed icon remains and your bed filter is preserved. Death-choice candidates appear even with this display off; temporary choice filters are restored afterward. Does not discover unknown beds.");
            public static readonly EmberSetting<bool> SleepAnyTime = EmberSetting.Toggle(Section, "Sleep Any Time", false, Cheat,
                "Cheat. Beds work at any time of day (the wake-up cooldown still applies). Off whenever the server blocks cheats.", safe: false);
            public static readonly EmberSetting<bool> TestingMode = EmberSetting.Toggle(Section, "Testing Mode", false, Cheat,
                "Cheat. Adds Fake Total Players and Simulate Players In Bed to the real counts. Off whenever the server blocks cheats.", safe: false);
            public static readonly EmberSetting<int> FakeTotalPlayers = EmberSetting.Integer(Section, "Fake Total Players", TestingOffsets.DefaultFakeTotalPlayers,
                0, TestingOffsets.MaxFake, ServerAuthoritative, "Players added to the count while Testing Mode is on.");
            public static readonly EmberSetting<int> SimulatePlayersInBed = EmberSetting.Integer(Section, "Simulate Players In Bed", TestingOffsets.DefaultSimulatePlayersInBed,
                0, TestingOffsets.MaxFake, ServerAuthoritative, "Sleepers added while Testing Mode is on (at most Fake Total Players).");
        }

        public static class Hud
        {
            public const string Section = "Sleep HUD";

            public static readonly EmberSetting<bool> Show = EmberSetting.Toggle(Section, "Show", true, Local,
                "Show the sleep panel under the minimap during the sleep window.");
            public static readonly EmberSetting<int> CompactAbove = EmberSetting.Integer(Section, "Compact Above", SleepHud.DefaultCompactAbove,
                SleepHud.MinCompactAbove, SleepHud.MaxCompactAbove, Local, "With more players than this, one pillow and a count are shown.");
        }

        public static class Stations
        {
            public const string Section = "Station Timers";

            public static readonly EmberSetting<bool> ProtectCookingTimers = EmberSetting.Toggle(Section, "Protect Cooking Timers", true, ServerAuthoritative,
                "During the partial sleep boost, food on cooking stations and in ovens cooks and burns at normal speed.");
            public static readonly EmberSetting<bool> ProtectSmelterTimers = EmberSetting.Toggle(Section, "Protect Smelter Timers", true, ServerAuthoritative,
                "During the partial sleep boost, smelters, blast furnaces, kilns and similar stations work at normal speed.");
            public static readonly EmberSetting<bool> ProtectCropTimers = EmberSetting.Toggle(Section, "Protect Crop Timers", true, ServerAuthoritative,
                "During the partial sleep boost, crops and saplings grow at normal speed.");
            public static readonly EmberSetting<bool> ProtectMeadTimers = EmberSetting.Toggle(Section, "Protect Mead Timers", true, ServerAuthoritative,
                "During the partial sleep boost, fermenters ferment at normal speed.");
            public static readonly EmberSetting<bool> ProtectPickableTimers = EmberSetting.Toggle(Section, "Protect Pickable Timers", true, ServerAuthoritative,
                "During the partial sleep boost, berry bushes and other pickables regrow at normal speed.");
        }

        public static class Ocean
        {
            public const string Section = "Ocean & Boat Physics";

            public static readonly EmberSetting<bool> DecoupleWaterWaveSpeed = EmberSetting.Toggle(Section, "Decouple Water Wave Speed", true, ServerAuthoritative,
                "While the night is accelerated, ocean waves move at normal real-time speed instead of boosted time.");
            public static readonly EmberSetting<bool> CalmOceanDuringSleep = EmberSetting.Toggle(Section, "Calm Ocean During Sleep", true, ServerAuthoritative,
                "Ocean waves are calmed and smoothed while the night is accelerated.");
            public static readonly EmberSetting<float> OceanCalmMultiplier = EmberSetting.Number(Section, "Ocean Calm Multiplier", 0.5f, 0f, 1f, ServerAuthoritative,
                "Wave height multiplier while calming is active (0 is flat, 1 is normal).");
            public static readonly EmberSetting<bool> PreventBoatSleepImpactDamage = EmberSetting.Toggle(Section, "Prevent Boat Sleep Impact Damage", true, ServerAuthoritative,
                "Boats take no spurious wave impact damage during accelerated sleep.");
            public static readonly EmberSetting<bool> SuppressBoatImpactScreenShake = EmberSetting.Toggle(Section, "Suppress Boat Impact Screen Shake", true, ServerAuthoritative,
                "No camera shake from fast wave wakes during accelerated sleep.");
        }

        public static class Torches
        {
            public const string Section = "Torches";

            public static readonly EmberSetting<bool> Enabled = EmberSetting.Toggle(Section, "Enabled", true, ServerAuthoritative,
                "Torches act like a campfire: warmth, resting and the bed's fire check.");
            public static readonly EmberSetting<string> FirePieces = EmberSetting.Text(Section, "Fire Pieces", FirePieceList.Default, ServerAuthoritative,
                "Comma list of piece prefab names that act as fire while lit.", FirePieceList.Normalize);
            public static readonly EmberSetting<bool> HeldTorchWarms = EmberSetting.Toggle(Section, "Held Torch Warms", true, ServerAuthoritative,
                "A lit torch held in hand keeps the player warm like a campfire.");
            public static readonly EmberSetting<bool> EnvironmentAffectsTorch = EmberSetting.Toggle(Section, "Environment Affects Torch", true, ServerAuthoritative,
                "Rain and storms make a held torch burn at Burn Rate.");
            public static readonly EmberSetting<bool> ColdBiomeAffectsTorch = EmberSetting.Toggle(Section, "Cold Biome Affects Torch", true, ServerAuthoritative,
                "The Mountains and the Deep North make a held torch burn at Burn Rate.");
            public static readonly EmberSetting<float> BurnRate = EmberSetting.Number(Section, "Burn Rate", TorchBurn.DefaultBurnRate, 0f, 1f, ServerAuthoritative,
                "Durability a held torch loses per second under the two rules above.");
            public static readonly EmberSetting<bool> EndlessTorches = EmberSetting.Toggle(Section, "Endless Torches", false, Cheat,
                "Cheat. Held torches lose no durability and listed fire pieces use no fuel. Off whenever the server blocks cheats.", safe: false);
            public static readonly EmberSetting<bool> FuelFreeFires = EmberSetting.Toggle(Section, "Fuel Free Fires", true, ServerAuthoritative,
                "The five standard placed torches and campfires burn without consuming fuel, including at zero stock; held torches lose no durability. Stored fuel and durability stay unchanged. Requires Ember's Torches module.");
            public static readonly EmberSetting<bool> SmokelessFires = EmberSetting.Toggle(Section, "Smokeless Fires", true, ServerAuthoritative,
                "The five standard placed torches, campfires and held torches produce no smoke. Other fires keep their behavior. Requires Ember's Torches module.");
            public static readonly EmberSetting<bool> TorchesNightOnly = EmberSetting.Toggle(Section, "Torches Night Only", false, ServerAuthoritative,
                "Player-built Torch Pieces light at night and go out by day, removing their warmth while off. Requires Ember's Torches module and steps aside for OpenKeep. Off relights torches this schedule put out; visit your bases before removing Ember.");
            public static readonly EmberSetting<string> TorchPieces = EmberSetting.Text(Section, "Torch Pieces", FirePieceList.Default, ServerAuthoritative,
                "Comma list of placed fire prefab names switched by Torches Night Only. Independent of Fire Pieces warmth and fuel-free fire eligibility.", FirePieceList.Normalize);
            public static readonly EmberSetting<float> TorchMargin = EmberSetting.Number(Section, "Torch Margin", 1f, 0f, TorchSchedule.MaxMargin, ServerAuthoritative,
                "In-game hours to light before nightfall and remain lit after daybreak. Zero follows the native night exactly.");
            public static readonly EmberSetting<string> TorchSwitchKey = EmberSetting.Text(Section, "Torch Switch Key", "O", Local,
                "While looking at a scheduled torch, keep it lit or return it to the night schedule. Requires ward access. Empty disables the key.", t => (t ?? "").Trim());
        }

        public static class Doors
        {
            public const string Section = "Doors";

            public static readonly EmberSetting<bool> AutoDoorsAllowed = EmberSetting.Toggle(Section, "Auto Doors Allowed", true, ServerAuthoritative,
                "Off turns automatic doors off for every player.");
            public static readonly EmberSetting<bool> EnableAutoDoor = EmberSetting.Toggle(Section, "Enable Auto Door", true, Local,
                "Doors open as you walk up and close behind you.");
            public static readonly EmberSetting<bool> KeepOpenWhileOthersNear = EmberSetting.Toggle(Section, "Keep Open While Others Near", true, Local,
                "Never close a door while another player is within its close distance.");
            public static readonly EmberSetting<string> ToggleKey = EmberSetting.Text(Section, "Toggle Key", "", Local,
                "Key that turns Enable Auto Door on or off. Empty: no key.", t => (t ?? "").Trim());
        }

        public static class Server
        {
            public const string Section = "Server";

            public static readonly EmberSetting<string> LockedPlayerSettings = EmberSetting.Text(Section, "Locked Player Settings", "", ServerAuthoritative,
                "Comma list of Ember player settings (Section.Key) the server forces to its own value for every player.", t => (t ?? "").Trim());
        }

        private static readonly Lazy<IReadOnlyDictionary<DoorKind, DoorKindSettings>> doors = new Lazy<IReadOnlyDictionary<DoorKind, DoorKindSettings>>(
            () => DoorKinds.All.ToDictionary(k => k, k => new DoorKindSettings(k)));

        // Deferred so that every nested holder is initialized first (Mono initializes the containing type eagerly).
        private static readonly Lazy<EmberSetting[]> all = new Lazy<EmberSetting[]>(() =>
            new EmberSetting[]
            {
                Sleep.Enabled, Sleep.UseVanillaSleepWindow, Sleep.SleepStart, Sleep.PartialSleepBoost, Sleep.BonusMultiplier,
                Sleep.BonusIncrementScale, Sleep.BoostFadeSeconds, Sleep.WakeAtWindowEnd, Sleep.SleepInAnyBed, Sleep.StandUpOnRespawn, Sleep.NearestOwnedBedRespawn, Sleep.QuickRespawn, Sleep.OwnedBedMapPins, Sleep.SleepAnyTime, Sleep.TestingMode,
                Sleep.FakeTotalPlayers, Sleep.SimulatePlayersInBed,
                Hud.Show, Hud.CompactAbove,
                Stations.ProtectCookingTimers, Stations.ProtectSmelterTimers, Stations.ProtectCropTimers, Stations.ProtectMeadTimers,
                Stations.ProtectPickableTimers,
                Ocean.DecoupleWaterWaveSpeed, Ocean.CalmOceanDuringSleep, Ocean.OceanCalmMultiplier, Ocean.PreventBoatSleepImpactDamage,
                Ocean.SuppressBoatImpactScreenShake,
                Torches.Enabled, Torches.FirePieces, Torches.HeldTorchWarms, Torches.EnvironmentAffectsTorch,
                Torches.ColdBiomeAffectsTorch, Torches.BurnRate, Torches.EndlessTorches, Torches.FuelFreeFires, Torches.SmokelessFires,
                Torches.TorchesNightOnly, Torches.TorchPieces, Torches.TorchMargin, Torches.TorchSwitchKey,
                Doors.AutoDoorsAllowed, Doors.EnableAutoDoor, Doors.KeepOpenWhileOthersNear, Doors.ToggleKey,
            }
            .Concat(DoorKinds.All.SelectMany(k => doors.Value[k].Entries()))
            .Concat(new EmberSetting[] { Server.LockedPlayerSettings })
            .ToArray());

        private static readonly Lazy<Dictionary<string, EmberSetting>> byId = new Lazy<Dictionary<string, EmberSetting>>(
            () => all.Value.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every setting in file order.</summary>
        public static IReadOnlyList<EmberSetting> All => all.Value;

        /// <summary>A setting by <c>Section.Key</c> in any case; null when unknown.</summary>
        public static EmberSetting? Find(string? id) => id != null && byId.Value.TryGetValue(id.Trim(), out var setting) ? setting : null;

        /// <summary>The settings of one door kind.</summary>
        public static DoorKindSettings Door(DoorKind kind) =>
            doors.Value.TryGetValue(kind ?? throw new ArgumentNullException(nameof(kind)), out var settings) ? settings : throw new ArgumentException("Unknown door kind.", nameof(kind));
    }

    /// <summary>Cheat values (design 4.4): a blocked cheat evaluates to its safe (vanilla) value.</summary>
    public static class EmberCheats
    {
        public static T Effective<T>(EmberSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        /// <summary>S12: testing offsets in effect; off whenever the Testing Mode cheat is blocked or off.</summary>
        public static TestingOffsets Testing(bool locked, bool enabled, int fake, int inBed) =>
            Effective(EmberSettings.Sleep.TestingMode, locked, enabled)
                ? new TestingOffsets(true, EmberSettings.Sleep.FakeTotalPlayers.Normalize(fake), EmberSettings.Sleep.SimulatePlayersInBed.Normalize(inBed))
                : TestingOffsets.Off;
    }
}

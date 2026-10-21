// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Setting names and ranges follow design section 4; the weather filter switches keep NoFogBruh's key
// names and descriptions (adopted table in Weather/WeatherSwitches.cs) so its config imports one to one.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Tempest.Events;
using ExtraPotions.Tempest.Weather;
using static ExtraPotions.Tempest.Config.SettingScope;

namespace ExtraPotions.Tempest.Config
{
    /// <summary>Tempest's mirror of Crucible's Mischief categories for its two cheats (Hex gate, C13).</summary>
    public enum CheatCategory { Personal, World }

    /// <summary>Design 4.1, all ServerAuthoritative.</summary>
    public sealed class ScheduleSettings
    {
        const string S = "Schedule";

        internal ScheduleSettings()
        {
            Enabled = TempestSetting.Toggle(S, "Enabled", true, ServerAuthoritative,
                "Off: no new weather events start and a running one ends at once. The weather filter still applies.");
            BlizzardChance = TempestSetting.Number(S, "Blizzard Chance Per Day", 0.03f, 0, 1, ServerAuthoritative,
                "Chance that a blizzard starts on a given in-game day (rolled once a day at a random time).");
            StormChance = TempestSetting.Number(S, "Thunderstorm Chance Per Day", 0.05f, 0, 1, ServerAuthoritative,
                "Chance that a Severe Thunderstorm starts on a given in-game day.");
            DryStormChance = TempestSetting.Number(S, "Lightning Storm Chance", 0.05f, 0, 1, ServerAuthoritative,
                "Chance that a new thunderstorm is a Lightning Storm: the same wind, thunder and darkness with more lightning (Lightning Storm Strike Multiplier) "
                + "and no rain, so with Strike Starts Fires on its strikes can set things alight. Rolled once when each storm starts; changes apply to the next storm. "
                + "Host setting (server value applies to everyone). Default 0.05 (5%).");
            MinimumDays = TempestSetting.Integer(S, "Minimum Days Between Events", 5, 0, 100, ServerAuthoritative,
                "In-game days that must pass after an event started before the next one may start.");
            WarningSeconds = TempestSetting.Integer(S, "Warning Seconds", 300, 60, 900, ServerAuthoritative,
                "Length of the warning phase: pale haze first, then light snow or rain in the last minute.");
            ActiveMinSeconds = TempestSetting.Integer(S, "Active Min Seconds", 300, 60, 1800, ServerAuthoritative,
                "Shortest length of the event itself.");
            ActiveMaxSeconds = TempestSetting.Integer(S, "Active Max Seconds", 600, 60, 3600, ServerAuthoritative,
                "Longest length of the event itself; a value below Active Min Seconds counts as Active Min Seconds.");
            BlizzardBiomes = TempestSetting.Text(S, "Blizzard Biomes", "Meadows, BlackForest, DeepNorth", ServerAuthoritative,
                "Biomes a blizzard covers, separated by commas: Meadows, BlackForest, Swamp, Plains, DeepNorth. In the Deep North a blizzard is a whiteout: "
                + "the twilight snowstorm with thicker fog and full wind. Default: Meadows, BlackForest, DeepNorth." + NoHighBiomes,
                v => BiomeSets.Normalize(EventKind.Blizzard, v));
            StormBiomes = TempestSetting.Text(S, "Thunderstorm Biomes", "Meadows, BlackForest, Swamp, Plains, Ocean", ServerAuthoritative,
                "Biomes a Severe Thunderstorm covers, separated by commas: Meadows, BlackForest, Swamp, Plains, Ocean." + NoHighBiomes,
                v => BiomeSets.Normalize(EventKind.Thunderstorm, v));
            FogChance = TempestSetting.Number(S, "Dense Fog Chance", 0.04f, 0, 1, ServerAuthoritative,
                "Chance that Dense Fog starts on a given in-game day: thick fog that cuts sight to about 15 to 20 meters. No damage. "
                + "Each kind owns its own share of the daily roll, so this does not change the other kinds' chances (while all chances add up to at most 1). "
                + "Host setting (server value applies to everyone); applies from the next daily roll. Default 0.04 (4%).");
            FogBiomes = TempestSetting.Text(S, "Dense Fog Biomes", "Meadows, BlackForest, Swamp, Plains, Ocean", ServerAuthoritative,
                "Biomes Dense Fog covers, separated by commas: Meadows, BlackForest, Swamp, Plains, Ocean. Empty: Dense Fog never starts. "
                + "Host setting (server value applies to everyone). Default: all five." + NoHighBiomes, v => BiomeSets.Normalize(EventKind.DenseFog, v));
            AshStormChance = TempestSetting.Number(S, "Ash Storm Chance", 0.05f, 0, 1, ServerAuthoritative,
                "Chance that an Ash Storm starts on a given in-game day in the Ashlands: a heavy ash storm that burns players out in the open "
                + "every 5 seconds (Storm Hazards Affect Players and Storm Hazard Damage). A roof or an interior protects. "
                + "Host setting (server value applies to everyone); applies from the next daily roll. Default 0.05 (5%).");
            AshStormBiomes = TempestSetting.Text(S, "Ash Storm Biomes", "AshLands", ServerAuthoritative,
                "Biomes an Ash Storm covers: AshLands, or empty so it never starts. Host setting (server value applies to everyone). Default: AshLands." + NoHighBiomes,
                v => BiomeSets.Normalize(EventKind.AshStorm, v));
            MeteorShowerChance = TempestSetting.Number(S, "Meteor Shower Chance", 0.03f, 0, 1, ServerAuthoritative,
                "Chance that a Meteor Shower is rolled on a given in-game day. It always falls at night: rolled by day, its warning lasts until nightfall, "
                + "and it ends at dawn. Meteors land like strikes (Strikes Per Minute, Strike Damage, Strike Hurts Players, Strike Hits Trees, Base Clearance), "
                + "and with Strike Starts Fires on and no rain they set things alight. Host setting (server value applies to everyone); applies from the next daily roll. "
                + "Default 0.03 (3%).");
            MeteorShowerBiomes = TempestSetting.Text(S, "Meteor Shower Biomes", "Plains, AshLands", ServerAuthoritative,
                "Biomes a Meteor Shower covers, separated by commas: Plains, AshLands. Empty: it never starts. Host setting (server value applies to everyone). "
                + "Default: Plains, AshLands." + NoHighBiomes, v => BiomeSets.Normalize(EventKind.MeteorShower, v));
        }

        /// <summary>Appended to each biome list help: no Tempest event reaches the Mountain or Mistlands, only a blizzard the Deep North.</summary>
        const string NoHighBiomes = " Tempest events never occur in the Mountain or Mistlands, and only a blizzard reaches the Deep North; names a kind does not cover are dropped.";

        public TempestSetting<bool> Enabled { get; }
        public TempestSetting<float> BlizzardChance { get; }
        public TempestSetting<float> StormChance { get; }
        public TempestSetting<float> DryStormChance { get; }
        public TempestSetting<int> MinimumDays { get; }
        public TempestSetting<int> WarningSeconds { get; }
        public TempestSetting<int> ActiveMinSeconds { get; }
        public TempestSetting<int> ActiveMaxSeconds { get; }
        public TempestSetting<string> BlizzardBiomes { get; }
        public TempestSetting<string> StormBiomes { get; }
        public TempestSetting<float> FogChance { get; }
        public TempestSetting<string> FogBiomes { get; }
        public TempestSetting<float> AshStormChance { get; }
        public TempestSetting<string> AshStormBiomes { get; }
        public TempestSetting<float> MeteorShowerChance { get; }
        public TempestSetting<string> MeteorShowerBiomes { get; }

        public IEnumerable<TempestSetting> Entries() => new TempestSetting[]
        {
            Enabled, BlizzardChance, StormChance, DryStormChance, MinimumDays, WarningSeconds, ActiveMinSeconds, ActiveMaxSeconds, BlizzardBiomes, StormBiomes,
            FogChance, FogBiomes, AshStormChance, AshStormBiomes, MeteorShowerChance, MeteorShowerBiomes,
        };
    }

    /// <summary>Design 4.2, all ServerAuthoritative.</summary>
    public sealed class EffectSettings
    {
        const string S = "Effects";

        internal EffectSettings()
        {
            BlizzardFreezing = TempestSetting.Choice(S, "Blizzard Freezing", FreezeLevel.Freezing, ServerAuthoritative,
                "Freezing: as in the Mountains (shelter and fire protect). Cold: cold only. None: no cold effect from the blizzard.");
            StrikesPerMinute = TempestSetting.Integer(S, "Strikes Per Minute", 4, 0, 30, ServerAuthoritative,
                "Ground lightning strikes per minute around each player in a Severe Thunderstorm, and meteor impacts per minute in a Meteor Shower. 0: no strikes.");
            LightningStrikeMultiplier = TempestSetting.Number(S, "Lightning Storm Strike Multiplier", 2f, 1, 5, ServerAuthoritative,
                "Strikes Per Minute is multiplied by this during a Lightning Storm (rounded, at most 30 a minute). Applies at once. "
                + "Host setting (server value applies to everyone). Default 2.");
            StrikeDamage = TempestSetting.Number(S, "Strike Damage", 40, 0, 200, ServerAuthoritative,
                "Damage of one strike: lightning for a lightning strike, blunt for a meteor impact (half of it fire while Strike Starts Fires applies).");
            StrikeHurtsPlayers = TempestSetting.Toggle(S, "Strike Hurts Players", true, ServerAuthoritative,
                "Off: strikes hit only terrain, trees and creatures.");
            StrikeStartsFires = TempestSetting.Toggle(S, "Strike Starts Fires", false, ServerAuthoritative,
                "On: when it is not raining, each strike deals half of Strike Damage as fire and sets one burnable thing at the impact alight with the game's own fire: a tree (when Strike Hits Trees is on) or dry grass. "
                + "While Storm Hazards Affect Players is on, a player standing in a strike's ground lightning also gets the game's Burning effect and Storm Hazard Damage is split evenly between lightning and fire. "
                + "Rain cancels all of this: strikes then deal only lightning damage and the ground lightning only shocks. Thunderstorms are usually rainy, so fires are rare. "
                + "Fire spreads only where the game allows it (Ashlands or the Fire world modifier). No fire starts inside a ward, a shield or Base Clearance, and player-built pieces are never set alight. Meteor Shower impacts follow the same rules. Host setting: applies to every player in the world and takes effect at once. Default: off.");
            StrikeHitsTrees = TempestSetting.Toggle(S, "Strike Hits Trees", true, ServerAuthoritative,
                "Off: strikes leave trees alone.");
            BaseClearance = TempestSetting.Number(S, "Base Clearance", 20, 0, 100, ServerAuthoritative,
                "Meters around player-built pieces where no strike lands, in addition to base and ward areas.");
            HazardsAffectPlayers = TempestSetting.Toggle(S, "Storm Hazards Affect Players", true, ServerAuthoritative,
                "On: a player standing in the ground lightning a strike leaves behind (about 2.4 m around it, for up to 10 s) is shocked once a second with the game's Lightning effect and takes Storm Hazard Damage. "
                + "It stops when the strike fades or the storm ends. During an Ash Storm a player out in the open (not under a roof or indoors) burns every 5 seconds instead. Players in god mode or ghost mode are not affected. Host setting: applies to every player in the world and takes effect at once.");
            HazardDamage = TempestSetting.Number(S, "Storm Hazard Damage", 5, 0, 50, ServerAuthoritative,
                "Damage per second to a player standing in a strike's ground lightning; armor and resistances apply. All lightning, except while Strike Starts Fires is on and it is not raining: then half lightning and half fire (5 becomes 2.5 + 2.5). An Ash Storm burn deals this as fire damage every 5 seconds. 0: status effects only. Host setting: applies to every player in the world and takes effect at once.");
        }

        public TempestSetting<FreezeLevel> BlizzardFreezing { get; }
        public TempestSetting<int> StrikesPerMinute { get; }
        public TempestSetting<float> LightningStrikeMultiplier { get; }
        public TempestSetting<float> StrikeDamage { get; }
        public TempestSetting<bool> StrikeHurtsPlayers { get; }
        public TempestSetting<bool> StrikeStartsFires { get; }
        public TempestSetting<bool> StrikeHitsTrees { get; }
        public TempestSetting<float> BaseClearance { get; }
        public TempestSetting<bool> HazardsAffectPlayers { get; }
        public TempestSetting<float> HazardDamage { get; }

        public IEnumerable<TempestSetting> Entries() => new TempestSetting[]
            { BlizzardFreezing, StrikesPerMinute, LightningStrikeMultiplier, StrikeDamage, StrikeHurtsPlayers, StrikeStartsFires, StrikeHitsTrees, BaseClearance, HazardsAffectPlayers, HazardDamage };
    }

    /// <summary>Design 4.3, the player's own (Local; a server may lock them through C01).</summary>
    public sealed class NoticeSettings
    {
        const string S = "Notices";

        internal NoticeSettings()
        {
            WarningMessage = TempestSetting.Toggle(S, "Warning Message", true, Local, "A center message when a warning starts where you are. While a player window is open (inventory, map, menus, MeadWorks windows) the message goes to the message log only. When Cairn's weather display is on, Cairn shows it instead.");
            HudBadge = TempestSetting.Toggle(S, "HUD Badge", true, Local,
                "A small badge with the coming event and its countdown. Hidden while Cairn shows the warning on its weather HUD and while a player window is open; movable in the HUD layout editor.");
            WarningSound = TempestSetting.Toggle(S, "Warning Sound", true, Local, "A game sound when a warning starts where you are.");
        }

        public TempestSetting<bool> WarningMessage { get; }
        public TempestSetting<bool> HudBadge { get; }
        public TempestSetting<bool> WarningSound { get; }

        public IEnumerable<TempestSetting> Entries() => new TempestSetting[] { WarningMessage, HudBadge, WarningSound };
    }

    /// <summary>Design 4.4: both cheats are toggles whose safe value is off.</summary>
    public sealed class CheatSettings
    {
        const string S = "Cheats";

        internal CheatSettings()
        {
            WeatherCommands = TempestSetting.Toggle(S, "Weather Commands", false, Cheat,
                "Allows the tempest console verbs that summon or stop a weather event for everyone (status is always available).", safe: false);
            PersonalOverride = TempestSetting.Toggle(S, "Personal Weather Override", false, Cheat,
                "Allows 'tempest override <environment> [freeze]': your own sky only, never the shared event; cleared when an event starts where you are.", safe: false);
        }

        public TempestSetting<bool> WeatherCommands { get; }
        public TempestSetting<bool> PersonalOverride { get; }

        public IEnumerable<TempestSetting> Entries() => new TempestSetting[] { WeatherCommands, PersonalOverride };
    }

    /// <summary>TP11: the weather filter (NoFogBruh's switches, Cairn Q2), all ServerAuthoritative.</summary>
    public sealed class FilterSettings
    {
        internal FilterSettings()
        {
            Switches = WeatherSwitches.All.ToDictionary(s => s,
                s => TempestSetting.Toggle(s.Section, s.Key, true, ServerAuthoritative, s.Description + " Off: the game never chooses this weather."));
            OtherDisabled = TempestSetting.Text("Weather Filter - Other", "Disabled Environments", "", ServerAuthoritative,
                "Other environment names (for example from other mods) the game should never choose, separated by commas. Names containing 'clear' are ignored.",
                WeatherFilter.NormalizeList);
        }

        public IReadOnlyDictionary<WeatherSwitch, TempestSetting<bool>> Switches { get; }
        public TempestSetting<string> OtherDisabled { get; }

        public IEnumerable<TempestSetting> Entries() => WeatherSwitches.All.Select(s => (TempestSetting)Switches[s]).Append(OtherDisabled);
    }

    /// <summary>Every Tempest setting, in config file order.</summary>
    public static class TempestSettings
    {
        /// <summary>Clearing length (design 1.2): fixed, not a setting.</summary>
        public const int ClearingSeconds = 60;

        public static ScheduleSettings Schedule { get; } = new ScheduleSettings();
        public static EffectSettings Effects { get; } = new EffectSettings();
        public static NoticeSettings Notices { get; } = new NoticeSettings();
        public static CheatSettings Cheats { get; } = new CheatSettings();
        public static FilterSettings Filter { get; } = new FilterSettings();

        public static IReadOnlyList<TempestSetting> All { get; } = Schedule.Entries().Concat(Effects.Entries()).Concat(Notices.Entries())
            .Concat(Cheats.Entries()).Concat(Filter.Entries()).ToList().AsReadOnly();

        static readonly Dictionary<string, TempestSetting> ById = All.ToDictionary(s => s.Id, StringComparer.Ordinal);

        public static TempestSetting? Find(string id) => id != null && ById.TryGetValue(id, out var s) ? s : null;

        public static CheatCategory CategoryOf(TempestSetting cheat)
        {
            if (cheat == Cheats.WeatherCommands) return CheatCategory.World;
            if (cheat == Cheats.PersonalOverride) return CheatCategory.Personal;
            throw new ArgumentException(cheat?.Id + " is not a Tempest cheat.", nameof(cheat));
        }

        /// <summary>Schedule rules from the effective values the plugin read.</summary>
        public static ScheduleRules Rules(bool enabled, float blizzardChance, float stormChance, int minimumDays, int warning, int activeMin, int activeMax,
            string blizzardBiomes, string stormBiomes, float dryStormChance = 0, float fogChance = 0, string? fogBiomes = null, float ashStormChance = 0,
            string? ashStormBiomes = null, float meteorShowerChance = 0, string? meteorShowerBiomes = null) =>
            new ScheduleRules(enabled, blizzardChance, stormChance, minimumDays, warning, activeMin, activeMax, ClearingSeconds,
                BiomeSets.Parse(EventKind.Blizzard, blizzardBiomes), BiomeSets.Parse(EventKind.Thunderstorm, stormBiomes), dryStormChance,
                fogChance, BiomeSets.Parse(EventKind.DenseFog, fogBiomes), ashStormChance, BiomeSets.Parse(EventKind.AshStorm, ashStormBiomes),
                meteorShowerChance, BiomeSets.Parse(EventKind.MeteorShower, meteorShowerBiomes));
    }

    /// <summary>A blocked cheat evaluates to its safe value.</summary>
    public static class TempestCheats
    {
        public static T Effective<T>(TempestSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }
    }
}

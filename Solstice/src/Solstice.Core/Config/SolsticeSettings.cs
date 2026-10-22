// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The setting names are Solstice's own; the effect defaults come from the adopted SeasonEffects table.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Effects;
using static ExtraPotions.Solstice.Config.SettingScope;

namespace ExtraPotions.Solstice.Config
{
    /// <summary>Solstice's mirror of Crucible's Mischief categories for its cheat (Hex gate).</summary>
    public enum CheatCategory { Personal, World }

    /// <summary>The Season Override cheat: Off, or the season the world is held in.</summary>
    public enum SeasonOverride { Off, Spring, Summer, Fall, Winter }

    public static class SeasonOverrides
    {
        /// <summary>The season to hold, or null for Off.</summary>
        public static Season? ToSeason(this SeasonOverride value) =>
            value == SeasonOverride.Off ? (Season?)null : (Season)((int)value - 1);
    }

    /// <summary>On or off for the whole product, decided by the server.</summary>
    public sealed class GeneralSettings
    {
        const string S = "General";

        internal GeneralSettings()
        {
            Enabled = SolsticeSetting.Toggle(S, "Enabled", true, ServerAuthoritative,
                "Off: no season effects and no seasonal weather; the world runs on the game's own rules. The season is still reported to other mods.");
        }

        public SolsticeSetting<bool> Enabled { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[] { Enabled };
    }

    /// <summary>The length of each season in world days, all ServerAuthoritative.</summary>
    public sealed class CalendarSettings
    {
        const string S = "Calendar";
        readonly SolsticeSetting<int>[] days = new SolsticeSetting<int>[Seasons.Count];

        internal CalendarSettings()
        {
            foreach (var season in Seasons.All)
                days[(int)season] = SolsticeSetting.Integer(S, "Days In " + season, SeasonEffects.Defaults(season).DaysInSeason, 1, 100, ServerAuthoritative,
                    "Length of " + season.ToString().ToLowerInvariant() + " in in-game days. The year is the four seasons in turn. The season already underway keeps its start day. A shorter length can end it on the next morning, and the new lengths apply to the seasons that follow.");
        }

        public SolsticeSetting<int> DaysIn(Season season) => days[(int)season];

        public IEnumerable<SolsticeSetting> Entries() => days;
    }

    /// <summary>The effects of one season, all ServerAuthoritative; the section is the season's name.</summary>
    public sealed class SeasonGroup
    {
        readonly Season season;

        internal SeasonGroup(Season season)
        {
            this.season = season;
            var d = SeasonEffects.Defaults(season);
            var s = season.ToString();
            var name = s.ToLowerInvariant();
            PlantsGrowth = Multiplier(s, "Plants Growth Multiplier", d.PlantsGrowthMultiplier, "How fast plants and crops grow and ripen in " + name + ". 1 is normal; 0 stops growth.");
            Beehive = Multiplier(s, "Beehive Production Multiplier", d.BeehiveProductionMultiplier, "How fast beehives make honey in " + name + ". 1 is normal; 0 stops them.");
            FoodDrain = Multiplier(s, "Food Drain Multiplier", d.FoodDrainMultiplier, "How fast eaten food runs out in " + name + ". 1 is normal; below 1 food lasts longer.");
            StaminaDrain = Multiplier(s, "Stamina Drain Multiplier", d.StaminaDrainMultiplier, "How much stamina your actions use in " + name + ". 1 is normal; below 1 you tire more slowly.");
            FireplaceDrain = Multiplier(s, "Fireplace Drain Multiplier", d.FireplaceDrainMultiplier, "How fast fireplaces, braziers, smelters and cooking fires burn their fuel in " + name + ". 1 is normal; above 1 they burn faster.");
            SapCollecting = Multiplier(s, "Sap Collecting Speed Multiplier", d.SapCollectingSpeedMultiplier, "How fast sap extractors fill in " + name + ". 1 is normal.");
            RestedDuration = Multiplier(s, "Rested Buff Duration Multiplier", d.RestedBuffDurationMultiplier, "How long the Rested effect lasts in " + name + ". 1 is normal.");
            WindIntensity = Multiplier(s, "Wind Intensity Multiplier", d.WindIntensityMultiplier, "How strong the wind is in " + name + ". 1 is normal.");
            RainProtection = SolsticeSetting.Toggle(s, "Rain Protection", d.RainProtection, ServerAuthoritative,
                "On: rain does not wear down roofed or unroofed buildings in " + name + " (snow replaces the rain).");
        }

        static SolsticeSetting<float> Multiplier(string section, string key, float value, string description) =>
            SolsticeSetting.Number(section, key, value, 0f, 10f, ServerAuthoritative, description);

        public Season Season => season;
        public SolsticeSetting<float> PlantsGrowth { get; }
        public SolsticeSetting<float> Beehive { get; }
        public SolsticeSetting<float> FoodDrain { get; }
        public SolsticeSetting<float> StaminaDrain { get; }
        public SolsticeSetting<float> FireplaceDrain { get; }
        public SolsticeSetting<float> SapCollecting { get; }
        public SolsticeSetting<float> RestedDuration { get; }
        public SolsticeSetting<float> WindIntensity { get; }
        public SolsticeSetting<bool> RainProtection { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[]
            { PlantsGrowth, Beehive, FoodDrain, StaminaDrain, FireplaceDrain, SapCollecting, RestedDuration, WindIntensity, RainProtection };

        /// <summary>The season's effects: the reference table with the wired effects replaced by the values read.</summary>
        public SeasonEffects Resolve(Func<SolsticeSetting<float>, float> number, Func<SolsticeSetting<bool>, bool> toggle)
        {
            var e = SeasonEffects.Defaults(season);
            e.PlantsGrowthMultiplier = number(PlantsGrowth);
            e.BeehiveProductionMultiplier = number(Beehive);
            e.FoodDrainMultiplier = number(FoodDrain);
            e.StaminaDrainMultiplier = number(StaminaDrain);
            e.FireplaceDrainMultiplier = number(FireplaceDrain);
            e.SapCollectingSpeedMultiplier = number(SapCollecting);
            e.RestedBuffDurationMultiplier = number(RestedDuration);
            e.WindIntensityMultiplier = number(WindIntensity);
            e.RainProtection = toggle(RainProtection);
            return e;
        }
    }

    public sealed class SeasonSettingGroups
    {
        readonly SeasonGroup[] groups = Seasons.All.Select(s => new SeasonGroup(s)).ToArray();

        internal SeasonSettingGroups() { }

        public SeasonGroup For(Season season) => groups[(int)season];

        public IEnumerable<SolsticeSetting> Entries() => groups.SelectMany(g => g.Entries());
    }

    /// <summary>Where the seasons do not reach and how crops live through winter, all ServerAuthoritative; the reference defaults.</summary>
    public sealed class ProtectionSettings
    {
        const string S = "Protection";

        internal ProtectionSettings()
        {
            HeatProtectsCrops = SolsticeSetting.Toggle(S, "Heat Protects Crops", true, ServerAuthoritative,
                "On: crops near a fire (heat) do not die of cold in a winter that stops growth.");
            ShieldProtects = SolsticeSetting.Toggle(S, "Shield Generator Protects", true, ServerAuthoritative,
                "On: places inside a shield generator dome are outside the seasonal effects, good and bad.");
            ShieldWinterOnly = SolsticeSetting.Toggle(S, "Shield Generator Protects In Winter Only", true, ServerAuthoritative,
                "On: the dome protects in winter only. Off: it protects in every season.");
            WinterHardyCrops = SolsticeSetting.Text(S, "Winter Hardy Crops", "Pickable_Carrot,Pickable_Barley,Pickable_Barley_Wild,Pickable_Flax,Pickable_Flax_Wild,Pickable_Thistle,Pickable_Mushroom_Magecap",
                ServerAuthoritative, "Grown crop prefab names, separated by commas, whose plants live through a winter that stops growth. Plants that tolerate cold always do.", WinterCrops.Normalize);
            CropsPerish = SolsticeSetting.Toggle(S, "Crops Perish", true, ServerAuthoritative,
                "On: ripe crops on the control list can freeze and perish in a winter that stops growth. The host or server sets this for everyone. A crop that perishes drops its normal harvest at the crop. A crop that survives stays ripe to pick.");
            PerishFromDay = SolsticeSetting.Integer(S, "Perish From Day", 3, 1, 100, ServerAuthoritative,
                "The winter day a controlled crop starts freezing. Earlier days it is only out in the cold. The host or server sets this for everyone.");
            FreezeSeconds = SolsticeSetting.Number(S, "Freeze Seconds", 120f, 0f, 3600f, ServerAuthoritative,
                "How many real seconds a controlled crop freezes before it either survives or perishes. Sleep and time skips do not shorten this grace period. The host or server sets this for everyone.");
            WinterHarvestChance = SolsticeSetting.Number(S, "Winter Harvest Chance", 0.33f, 0f, 1f, ServerAuthoritative,
                "The chance, from 0 to 1, that a controlled crop stays ripe after it finishes freezing. The host or server sets this for everyone. 0 always perishes. 1 always stays ripe.");
            CropsToControl = SolsticeSetting.Text(S, "Crops To Control",
                "Pickable_Barley,Pickable_Barley_Wild,Pickable_Dandelion,Pickable_Flax,Pickable_Flax_Wild,Pickable_SeedCarrot,Pickable_SeedOnion,Pickable_SeedTurnip,Pickable_Thistle,Pickable_Turnip",
                ServerAuthoritative, "Prefab names, separated by commas, of ripe crops that can freeze and perish. The host or server sets this for everyone.", WinterCrops.Normalize);
        }

        public SolsticeSetting<bool> HeatProtectsCrops { get; }
        public SolsticeSetting<bool> ShieldProtects { get; }
        public SolsticeSetting<bool> ShieldWinterOnly { get; }
        public SolsticeSetting<string> WinterHardyCrops { get; }
        public SolsticeSetting<bool> CropsPerish { get; }
        public SolsticeSetting<int> PerishFromDay { get; }
        public SolsticeSetting<float> FreezeSeconds { get; }
        public SolsticeSetting<float> WinterHarvestChance { get; }
        public SolsticeSetting<string> CropsToControl { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[]
        {
            HeatProtectsCrops, ShieldProtects, ShieldWinterOnly, WinterHardyCrops,
            CropsPerish, PerishFromDay, FreezeSeconds, WinterHarvestChance, CropsToControl
        };
    }

    /// <summary>The season status and warm-clothes overheat, all ServerAuthoritative.</summary>
    public sealed class ClimateSettings
    {
        const string S = "Climate";

        internal ClimateSettings()
        {
            SeasonStatus = SolsticeSetting.Toggle(S, "Season Status", true, ServerAuthoritative,
                "On: the season's status effect applies outdoors, except in the Ashlands and the Deep North. The host or server sets this for everyone. A shield dome does not block it.");
            WarmClothesOverheat = SolsticeSetting.Toggle(S, "Warm Clothes Overheat", true, ServerAuthoritative,
                "On: two or more frost-resistant clothes in a hot season slow stamina and Eitr recovery, unless the player is indoors, sheltered, in cold weather or has eaten a cooling food. The host or server sets this for everyone.");
            CoolingFoods = SolsticeSetting.Text(S, "Cooling Foods", "Eyescream,$item_eyescream", ServerAuthoritative,
                "Food names or prefab names, separated by commas, that stop the warm-clothes overheat. The host or server sets this for everyone. The default is Eyescream.", WinterCrops.Normalize);
        }

        public SolsticeSetting<bool> SeasonStatus { get; }
        public SolsticeSetting<bool> WarmClothesOverheat { get; }
        public SolsticeSetting<string> CoolingFoods { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[] { SeasonStatus, WarmClothesOverheat, CoolingFoods };
    }

    /// <summary>The player's own notices (Local).</summary>
    public sealed class NoticeSettings
    {
        const string S = "Notices";

        internal NoticeSettings()
        {
            SeasonMessage = SolsticeSetting.Toggle(S, "Season Message", true, Local, "A center message when a new season starts. Local to this player.");
        }

        public SolsticeSetting<bool> SeasonMessage { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[] { SeasonMessage };
    }

    /// <summary>Colors and the season plate on this client. The server does not override them.</summary>
    public sealed class VisualSettings
    {
        const string S = "Visuals";

        internal VisualSettings()
        {
            WorldTint = SolsticeSetting.Toggle(S, "World Tint", true, Local,
                "Solstice colors grass, leaves and the ground for the season on this client. Spring and summer stay green and fall turns leaves amber. Winter color follows Snow Cover. Local to this player. The server does not override it. Takes effect at once. Off restores the game's colors.");
            SnowCover = SolsticeSetting.Toggle(S, "Snow Cover", true, Local,
                "In winter, Solstice covers Meadows, Black Forest, Swamp and Plains ground with the game's mountain snow, following the winter day. Grass thins and turns frosty, flowers drop out, and leaves take a light snow while trunks stay brown. Open ground stays a little under full mountain white. It also raises the game's own snow level on materials that already have one. Local to this player. The server does not override it. Solstice does not save snow onto buildings and does not add a snow mesh. Takes effect at once. Off restores the ground, the grass and the textures.");
            FrozenWater = SolsticeSetting.Toggle(S, "Frozen Water", true, Local,
                "On the winter days when shallow water freezes, Solstice tints the shallow color, and the top color and foam when the water has them, toward an icy blue. The water stays partly transparent, and the ripples are spaced out. Local to this player. The server does not override it. A missing property is left as it is. Solstice does not add ice you can walk on. Takes effect at once. Off restores the water.");
            MinimapTint = SolsticeSetting.Toggle(S, "Minimap Tint", true, Local,
                "Solstice tints the small map and the large map for the season. Terrain, pins and fog stay the game's. Local to this player. The server does not override it. Takes effect at once. Off restores the maps.");
            SeasonIndicator = SolsticeSetting.Toggle(S, "Season Indicator", true, Local,
                "Solstice shows the current season and day in a small plate under the compass. Local to this player. The server does not override it. The plate stays off the compass and the status effect row. Takes effect at once.");
            SeasonIcons = SolsticeSetting.Toggle(S, "Season Icons", true, Local,
                "Solstice draws a small mark for the season on the season plate and on the season status. The marks are drawn by Solstice in the Meadworks colors. Local to this player. The server does not override it. Takes effect at once. Off removes the mark from the plate. Winter keeps the Solstice winter mark on the status so it stays visible on a bright sky. Other seasons use the game's status icon.");
            HeatMeter = SolsticeSetting.Toggle(S, "Heat Meter", true, Local,
                "Solstice shows a small meter of warm-clothes heat in a hot season. It fills while frost-resistant clothes are worn outdoors and empties indoors, in shelter, in cold weather, or after a cooling food. Two or more pieces is the overheat that slows stamina and Eitr recovery. Local to this player. The server does not override it. The meter stays off the compass, the status row, notices and the hotbar. Takes effect at once. A narrow window leaves the word off when it would be smaller than 8 pixels, and a window at least 1280 wide keeps the word.");
        }

        public SolsticeSetting<bool> WorldTint { get; }
        public SolsticeSetting<bool> SnowCover { get; }
        public SolsticeSetting<bool> FrozenWater { get; }
        public SolsticeSetting<bool> MinimapTint { get; }
        public SolsticeSetting<bool> SeasonIndicator { get; }
        public SolsticeSetting<bool> SeasonIcons { get; }
        public SolsticeSetting<bool> HeatMeter { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[] { WorldTint, SnowCover, FrozenWater, MinimapTint, SeasonIndicator, SeasonIcons, HeatMeter };
    }

    /// <summary>Seasonal music on this client. The server does not override it, and a dedicated server does not play it.</summary>
    public sealed class MusicSettings
    {
        const string S = "Music";

        internal MusicSettings()
        {
            SeasonalMusic = SolsticeSetting.Toggle(S, "Seasonal Music", true, Local,
                "Solstice plays one of the game's own music tracks for the season, such as a brighter plains or meadow track in summer and a quieter mountain or Deep North track in winter. Combat, bosses, locations, sailing, morning, evening and the home track are left alone. Local to this player. The server does not override it. Takes effect at once. Off returns to the game's music. A dedicated server does not play music.");
        }

        public SolsticeSetting<bool> SeasonalMusic { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[] { SeasonalMusic };
    }

    /// <summary>The one cheat: hold the world in a season. Its safe value is Off.</summary>
    public sealed class CheatSettings
    {
        const string S = "Cheats";

        internal CheatSettings()
        {
            SeasonOverride = SolsticeSetting.CheatChoice(S, "Season Override", Config.SeasonOverride.Off,
                "Holds the world in one season for everyone, whatever the calendar says (Off lets the calendar run). Spring, Summer, Fall or Winter.");
        }

        public SolsticeSetting<SeasonOverride> SeasonOverride { get; }

        public IEnumerable<SolsticeSetting> Entries() => new SolsticeSetting[] { SeasonOverride };
    }

    /// <summary>Every Solstice setting, in config file order.</summary>
    public static class SolsticeSettings
    {
        public static GeneralSettings General { get; } = new GeneralSettings();
        public static CalendarSettings Calendar { get; } = new CalendarSettings();
        public static SeasonSettingGroups Seasons { get; } = new SeasonSettingGroups();
        public static ProtectionSettings Protection { get; } = new ProtectionSettings();
        public static ClimateSettings Climate { get; } = new ClimateSettings();
        public static NoticeSettings Notices { get; } = new NoticeSettings();
        public static VisualSettings Visuals { get; } = new VisualSettings();
        public static MusicSettings Music { get; } = new MusicSettings();
        public static CheatSettings Cheats { get; } = new CheatSettings();

        public static IReadOnlyList<SolsticeSetting> All { get; } = General.Entries().Concat(Calendar.Entries()).Concat(Seasons.Entries())
            .Concat(Protection.Entries()).Concat(Climate.Entries()).Concat(Notices.Entries()).Concat(Visuals.Entries()).Concat(Music.Entries()).Concat(Cheats.Entries()).ToList().AsReadOnly();

        static readonly Dictionary<string, SolsticeSetting> ById = All.ToDictionary(s => s.Id, StringComparer.Ordinal);

        public static SolsticeSetting? Find(string id) => id != null && ById.TryGetValue(id, out var s) ? s : null;

        public static CheatCategory CategoryOf(SolsticeSetting cheat)
        {
            if (cheat == Cheats.SeasonOverride) return CheatCategory.World;
            throw new ArgumentException(cheat?.Id + " is not a Solstice cheat.", nameof(cheat));
        }

        /// <summary>The shield rules from the protection settings, read through <paramref name="toggle"/>.</summary>
        public static ProtectionRules RulesFrom(Func<SolsticeSetting<bool>, bool> toggle) =>
            new ProtectionRules(toggle(Protection.ShieldProtects), toggle(Protection.ShieldWinterOnly));

        /// <summary>The calendar from the season length settings, read through <paramref name="days"/>.</summary>
        public static SeasonCalendar CalendarFrom(Func<SolsticeSetting<int>, int> days) =>
            SeasonEffects.CalendarOf(season => days(Calendar.DaysIn(season)));
    }

    /// <summary>A blocked cheat evaluates to its safe value.</summary>
    public static class SolsticeCheats
    {
        public static T Effective<T>(SolsticeSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }
    }
}

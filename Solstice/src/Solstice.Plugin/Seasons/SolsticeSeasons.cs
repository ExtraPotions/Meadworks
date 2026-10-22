// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the current season of this session. The calendar arithmetic and the saved clock are Solstice.Core.
using System;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Effects;
using ExtraPotions.Solstice.Integration;
using ExtraPotions.Solstice.Localization;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// The season players are in. The server writes a world-day clock; clients read it and do not invent one.
    /// The Season Override cheat changes the displayed season and is not written into the clock.
    /// Main thread only.
    /// </summary>
    public static class SolsticeSeasons
    {
        static MeadworksLog? log;
        static bool dirty = true;
        static int lastDay = int.MinValue;
        static Season? lastHeld;
        static Season? lastSeason;
        static SeasonCalendar calendar = SeasonCalendar.Default;
        static readonly SeasonViewRevision Revision = new SeasonViewRevision();
        static readonly SeasonEffects?[] Table = new SeasonEffects?[Seasons.Count];

        /// <summary>The reading for <c>meadworks.seasons.view</c>; unavailable until the clock is known.</summary>
        public static SeasonView View { get; private set; }

        /// <summary>The saved clock, without the cheat. Null until the world key is known.</summary>
        public static SeasonClock? Clock => SeasonClockSync.Current;

        /// <summary>The effects of the displayed season; null while Solstice is off or its Enabled setting is off.</summary>
        public static SeasonEffects? Effects { get; private set; }

        /// <summary>The season the Season Override cheat holds the world in, or null.</summary>
        public static Season? Held { get; private set; }

        /// <summary>The displayed season, including the cheat; null while the clock is unknown.</summary>
        public static Season? Current => View.Available ? (Season)View.Season : (Season?)null;

        public static SeasonCalendar Calendar => calendar;

        /// <summary>The shield dome rules.</summary>
        public static ProtectionRules Protection { get; private set; } = new ProtectionRules(true, true);

        public static bool HeatProtectsCrops { get; private set; } = true;

        public static WinterCrops HardyCrops { get; private set; } = WinterCrops.Parse(SolsticeSettings.Protection.WinterHardyCrops.Default);

        public static bool CropsPerish { get; private set; } = true;

        public static int PerishFromDay { get; private set; } = CropWinter.DefaultPerishDay;

        public static float FreezeSeconds { get; private set; } = CropWinter.DefaultFreezeSeconds;

        public static float HarvestChance { get; private set; } = CropWinter.DefaultHarvestChance;

        public static WinterCrops ControlledCrops { get; private set; } = WinterCrops.Parse(SolsticeSettings.Protection.CropsToControl.Default);

        public static bool SeasonStatusEnabled { get; private set; } = true;

        public static bool WarmOverheat { get; private set; } = true;

        public static string CoolingFoods { get; private set; } = SolsticeSettings.Climate.CoolingFoods.Default;

        /// <summary>Raised when the displayed season changes (not on the first reading of a session).</summary>
        public static event Action<Season>? SeasonChanged;

        public static void Initialize(MeadworksLog logger) => log = logger;

        public static void LogWarning(string message) => log?.Warning(message);

        /// <summary>The configured effects of a season once the clock is known, including while Enabled is off. Null before that.</summary>
        public static SeasonEffects? EffectsOf(Season season) => View.Available ? Table[(int)season] : null;

        /// <summary><c>meadworks.seasons.facts</c> 1.0. Empty until the clock is known.</summary>
        public static string Facts()
        {
            var clock = Clock;
            if (clock == null || !View.Available) return "";
            var shown = (Season)View.Season;
            var effects = EffectsOf(shown) ?? SeasonEffects.Defaults(shown);
            int night = NightLength.For(clock.Value.Calendar, NightOf, shown, View.DayInSeason, true);
            return SeasonFacts.Format(new SeasonFactValues(true, View.Season, (int)clock.Value.Season, View.DayInSeason, View.DaysInSeason,
                View.DayOfYear, View.YearLength, clock.Value.WorldDay, clock.Value.AnchorDay, clock.Value.Revision,
                Held.HasValue, Effects != null, night, NightLength.DayStartFraction(night),
                effects.WoodFromTreesMultiplier, effects.MeatFromAnimalsMultiplier, effects.LivestockProcreationMultiplier,
                effects.TorchAsFiresource, effects.TorchDurabilityDrain, effects.OverheatIn2WarmClothes, effects.PlantsGrowthMultiplier,
                ClimateRules.SnowCover(shown, View.DayInSeason, View.DaysInSeason, false, false),
                ClimateRules.WaterFrozen(shown, View.DayInSeason)));
        }

        static int NightOf(Season season) => (EffectsOf(season) ?? SeasonEffects.Defaults(season)).NightLength;

        public static void Invalidate() => dirty = true;

        public static void Tick()
        {
            var settings = Plugin.Settings;
            var envMan = EnvMan.instance;
            if (settings == null || envMan == null || !SolsticeModules.Active(SolsticeModule.Seasons))
            {
                SeasonClockSync.ClearSession();
                Clear();
                return;
            }
            int day = envMan.GetCurrentDay();
            bool clockChanged = SeasonClockSync.Advance(day, settings.Calendar());
            var held = settings.HeldSeason(out var blocked);
            if (!dirty && !clockChanged && day == lastDay && held == lastHeld) return;
            if (blocked) settings.ReportBlockedCheat(SolsticeSettings.Cheats.SeasonOverride, SolsticeText.Text("cheat_season_override"));
            dirty = false;
            lastDay = day;
            lastHeld = held;
            ReadSettings(settings);
            Held = held;
            var clock = Clock;
            if (clock == null)
            {
                View = default;
                Effects = null;
                lastSeason = null;
                return;
            }
            calendar = clock.Value.Calendar;
            foreach (var s in Seasons.All) Table[(int)s] = settings.Effects(s);
            View = Revision.Stamp(SeasonView.FromClock(clock.Value, held));
            var season = (Season)View.Season;
            Effects = settings.Get(SolsticeSettings.General.Enabled) ? Table[(int)season] : null;

            var previous = lastSeason;
            lastSeason = season;
            SeasonProtection.Reset();
            if (previous.HasValue && previous.Value != season) OnSeasonChanged(season, settings);
        }

        static void ReadSettings(SolsticeConfig settings)
        {
            Protection = settings.Protection();
            HeatProtectsCrops = settings.Get(SolsticeSettings.Protection.HeatProtectsCrops);
            HardyCrops = WinterCrops.Parse(settings.Get(SolsticeSettings.Protection.WinterHardyCrops));
            CropsPerish = settings.Get(SolsticeSettings.Protection.CropsPerish);
            PerishFromDay = settings.Get(SolsticeSettings.Protection.PerishFromDay);
            FreezeSeconds = settings.Get(SolsticeSettings.Protection.FreezeSeconds);
            HarvestChance = settings.Get(SolsticeSettings.Protection.WinterHarvestChance);
            ControlledCrops = WinterCrops.Parse(settings.Get(SolsticeSettings.Protection.CropsToControl));
            SeasonStatusEnabled = settings.Get(SolsticeSettings.Climate.SeasonStatus);
            WarmOverheat = settings.Get(SolsticeSettings.Climate.WarmClothesOverheat);
            CoolingFoods = settings.Get(SolsticeSettings.Climate.CoolingFoods);
        }

        static void OnSeasonChanged(Season season, SolsticeConfig settings)
        {
            log?.Info("Season changed to " + season + " (day " + View.DayInSeason + " of " + View.DaysInSeason + ").");
            if (Effects != null) SolsticeWeather.RequestRefresh();
            if (log != null && Effects != null && settings.Get(SolsticeSettings.Notices.SeasonMessage) && Player.m_localPlayer != null)
                SolsticeText.Centre(SolsticeText.Format(SolsticeTextKeys.SeasonStarted, SolsticeText.Text(SolsticeTextKeys.SeasonName(season))), log);
            try { SeasonChanged?.Invoke(season); }
            catch (Exception error) { log?.Warning("Season change handler: " + error.GetType().Name); }
        }

        static void Clear()
        {
            SeasonProtection.Reset();
            View = default;
            Effects = null;
            Held = null;
            lastSeason = null;
            lastDay = int.MinValue;
            lastHeld = null;
            dirty = true;
        }

        public static void Shutdown()
        {
            Clear();
            SeasonChanged = null;
            log = null;
        }
    }
}

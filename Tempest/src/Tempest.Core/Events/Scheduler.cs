// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the authority's scheduler (design 1.2 and 4.1; WildWeather's "rare" events, behaviour only).
using System;

namespace ExtraPotions.Tempest.Events
{
    /// <summary>The schedule settings of design 4.1 as plain values (the plugin reads them from C01 at each tick).</summary>
    public sealed class ScheduleRules
    {
        public ScheduleRules(bool enabled, float blizzardChance, float stormChance, int minimumDaysBetween, int warningSeconds,
            int activeMinSeconds, int activeMaxSeconds, int clearingSeconds, WeatherBiome blizzardBiomes, WeatherBiome stormBiomes,
            float dryStormChance = 0, float fogChance = 0, WeatherBiome fogBiomes = WeatherBiome.None, float ashStormChance = 0,
            WeatherBiome ashStormBiomes = WeatherBiome.None, float meteorShowerChance = 0, WeatherBiome meteorShowerBiomes = WeatherBiome.None)
        {
            Enabled = enabled;
            BlizzardChance = Clamp01(blizzardChance);
            StormChance = Clamp01(stormChance);
            MinimumDaysBetween = Math.Max(0, minimumDaysBetween);
            WarningSeconds = Math.Max(0, Math.Min(TempestEvent.MaximumPhaseSeconds, warningSeconds));
            ActiveMinSeconds = Math.Max(1, Math.Min(TempestEvent.MaximumPhaseSeconds, activeMinSeconds));
            ActiveMaxSeconds = Math.Max(ActiveMinSeconds, Math.Min(TempestEvent.MaximumPhaseSeconds, activeMaxSeconds));
            ClearingSeconds = Math.Max(0, Math.Min(TempestEvent.MaximumPhaseSeconds, clearingSeconds));
            BlizzardBiomes = blizzardBiomes & BiomeSets.Allowed(EventKind.Blizzard);
            StormBiomes = stormBiomes & BiomeSets.Allowed(EventKind.Thunderstorm);
            DryStormChance = Clamp01(dryStormChance);
            FogChance = Clamp01(fogChance);
            FogBiomes = fogBiomes & BiomeSets.Allowed(EventKind.DenseFog);
            AshStormChance = Clamp01(ashStormChance);
            AshStormBiomes = ashStormBiomes & BiomeSets.Allowed(EventKind.AshStorm);
            MeteorShowerChance = Clamp01(meteorShowerChance);
            MeteorShowerBiomes = meteorShowerBiomes & BiomeSets.Allowed(EventKind.MeteorShower);
        }

        public bool Enabled { get; }
        public float BlizzardChance { get; }
        public float StormChance { get; }
        public int MinimumDaysBetween { get; }
        public int WarningSeconds { get; }
        public int ActiveMinSeconds { get; }

        /// <summary>Never below <see cref="ActiveMinSeconds"/> (design 4.1 "not below min").</summary>
        public int ActiveMaxSeconds { get; }

        public int ClearingSeconds { get; }
        public WeatherBiome BlizzardBiomes { get; }
        public WeatherBiome StormBiomes { get; }

        /// <summary>Chance that a new thunderstorm is dry (no rain).</summary>
        public float DryStormChance { get; }

        public float FogChance { get; }
        public WeatherBiome FogBiomes { get; }
        public float AshStormChance { get; }
        public WeatherBiome AshStormBiomes { get; }
        public float MeteorShowerChance { get; }
        public WeatherBiome MeteorShowerBiomes { get; }

        /// <summary>The daily chance of a kind. A Lightning Storm is not rolled on its own: it is a thunderstorm that rolled
        /// <see cref="DryStormChance"/>, so its daily chance is the thunderstorm's times that.</summary>
        public float Chance(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Blizzard: return BlizzardChance;
                case EventKind.Thunderstorm: return StormChance;
                case EventKind.LightningStorm: return StormChance * DryStormChance;
                case EventKind.DenseFog: return FogChance;
                case EventKind.AshStorm: return AshStormChance;
                case EventKind.MeteorShower: return MeteorShowerChance;
                default: return 0;
            }
        }

        public WeatherBiome Biomes(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Blizzard: return BlizzardBiomes;
                case EventKind.Thunderstorm:
                case EventKind.LightningStorm: return StormBiomes;
                case EventKind.DenseFog: return FogBiomes;
                case EventKind.AshStorm: return AshStormBiomes;
                case EventKind.MeteorShower: return MeteorShowerBiomes;
                default: return WeatherBiome.None;
            }
        }

        static float Clamp01(float v) => float.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }

    /// <summary>What the authority keeps between ticks and across restarts (held in the event record, TP03).</summary>
    public sealed class ScheduleState : IEquatable<ScheduleState>
    {
        /// <summary>The largest serial; a record past it is refused.</summary>
        public const int MaximumSerial = 1000000000;

        public ScheduleState(TempestEvent? current, long lastRolledDay, long lastStartDay, int nextSerial)
        {
            if (lastRolledDay < -1) throw new ArgumentException("Expected -1 or a day.", nameof(lastRolledDay));
            if (nextSerial < 1 || nextSerial > MaximumSerial) throw new ArgumentException("Serial out of range.", nameof(nextSerial));
            if (current != null && current.Serial >= nextSerial) throw new ArgumentException("The next serial must follow the current event.", nameof(nextSerial));
            Current = current;
            LastRolledDay = lastRolledDay;
            LastStartDay = lastStartDay;
            NextSerial = nextSerial;
        }

        /// <summary>No event yet, nothing rolled, no gap to respect.</summary>
        public static ScheduleState Empty => new ScheduleState(null, -1, NoStart, 1);

        /// <summary><see cref="LastStartDay"/> before any event.</summary>
        public const long NoStart = long.MinValue / 4;

        public TempestEvent? Current { get; }

        /// <summary>The last in-game day whose roll was used (-1: none).</summary>
        public long LastRolledDay { get; }

        /// <summary>The in-game day the last event started (<see cref="NoStart"/>: never).</summary>
        public long LastStartDay { get; }

        public int NextSerial { get; }

        public ScheduleState With(TempestEvent? current) => new ScheduleState(current, LastRolledDay, LastStartDay, NextSerial);

        public bool Equals(ScheduleState? other) => other != null && Equals(Current, other.Current) && LastRolledDay == other.LastRolledDay
            && LastStartDay == other.LastStartDay && NextSerial == other.NextSerial;

        public override bool Equals(object? obj) => obj is ScheduleState other && Equals(other);

        public override int GetHashCode() => (LastRolledDay.GetHashCode() * 397 ^ LastStartDay.GetHashCode()) * 397 ^ NextSerial;
    }

    public enum SchedulerOutcome { None, Started, Finished, Stopped, Busy, Refused }

    public readonly struct SchedulerResult
    {
        public SchedulerResult(ScheduleState state, SchedulerOutcome outcome)
        {
            State = state;
            Outcome = outcome;
        }

        public ScheduleState State { get; }
        public SchedulerOutcome Outcome { get; }
    }

    /// <summary>
    /// The authority's scheduler (design 1.2): once per in-game day at a seeded random time it rolls each kind against its
    /// chance, respecting the minimum gap and that a player is online in a biome the kind covers. Deterministic for a world
    /// seed, so a restart re-derives the same roll times.
    /// </summary>
    public static class Scheduler
    {
        const ulong RollSalt = 0x7465_6d70_6573_7401;
        const ulong LengthSalt = 0x7465_6d70_6573_7404;
        const ulong SeedSalt = 0x7465_6d70_6573_7405;
        const ulong DrySalt = 0x7465_6d70_6573_7406;
        const ulong PickSalt = 0x7465_6d70_6573_7407;

        public static long DayOf(double now, double dayLength) => dayLength > 0 && now >= 0 ? (long)Math.Floor(now / dayLength) : 0;

        /// <summary>The world time of <paramref name="day"/>'s roll: between 5 and 95 percent of the day.</summary>
        public static double RollTime(long worldSeed, long day, double dayLength) =>
            day * dayLength + (0.05 + 0.9 * SeededRandom.Unit((ulong)worldSeed, (ulong)day, RollSalt)) * dayLength;

        /// <summary>
        /// One authority tick. <paramref name="occupied"/> holds the biomes with at least one online player.
        /// Ends a finished event, clears everything while disabled, and uses each day's roll once. <paramref name="raided"/>
        /// holds the biomes of online players inside a vanilla random event's area (<see cref="VanillaEvents"/>): while the
        /// picked kind covers one of them the day's roll is kept, unused, and tried again on later ticks.
        /// </summary>
        public static SchedulerResult Tick(ScheduleState state, ScheduleRules rules, long worldSeed, double now, double dayLength, WeatherBiome occupied,
            WeatherBiome raided = WeatherBiome.None)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (state.Current != null && !rules.Enabled) return new SchedulerResult(state.With(null), SchedulerOutcome.Stopped);
            if (state.Current != null && Timeline.Finished(state.Current, now)) return new SchedulerResult(state.With(null), SchedulerOutcome.Finished);
            var day = DayOf(now, dayLength);
            if (day <= state.LastRolledDay || now < RollTime(worldSeed, day, dayLength)) return new SchedulerResult(state, SchedulerOutcome.None);
            var rolled = new ScheduleState(state.Current, day, state.LastStartDay, state.NextSerial);
            if (!rules.Enabled || state.Current != null || day - state.LastStartDay < rules.MinimumDaysBetween || state.NextSerial >= ScheduleState.MaximumSerial)
                return new SchedulerResult(rolled, SchedulerOutcome.None);
            var kind = Pick(rules, SeededRandom.Unit((ulong)worldSeed, (ulong)day, PickSalt));
            if (kind == null || (rules.Biomes(kind.Value) & occupied) == 0) return new SchedulerResult(rolled, SchedulerOutcome.None);
            if ((rules.Biomes(kind.Value) & raided) != 0) return new SchedulerResult(state, SchedulerOutcome.None);
            var length = rules.ActiveMinSeconds + (int)Math.Floor(SeededRandom.Unit((ulong)worldSeed, (ulong)day, LengthSalt) * (rules.ActiveMaxSeconds - rules.ActiveMinSeconds + 1));
            var seed = unchecked((int)SeededRandom.Next((ulong)worldSeed, (ulong)day, SeedSalt));
            var started = Start(rolled, rules, kind.Value, now, rules.WarningSeconds, Math.Min(length, rules.ActiveMaxSeconds), seed, dayLength, null, nightRule: true);
            // A Meteor Shower with no night in reach uses the day's roll for nothing.
            return started.Outcome == SchedulerOutcome.Started ? started : new SchedulerResult(rolled, SchedulerOutcome.None);
        }

        /// <summary>
        /// The daily weighted pick: each rolled kind (<see cref="EventKinds.Rolled"/>) with a chance above 0 and at least one
        /// biome owns a slice of [0, 1) as wide as its chance, in that fixed order; <paramref name="roll"/> falling in a slice
        /// picks that kind, past the last slice picks nothing. Each kind therefore starts on a given day with exactly its own
        /// chance while the chances add up to at most 1; above that the slices are scaled in proportion to fill [0, 1). The
        /// caller then checks that a player is in one of the picked kind's biomes, so an unoccupied kind never shifts the others.
        /// </summary>
        public static EventKind? Pick(ScheduleRules rules, double roll)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            double total = 0;
            foreach (var kind in EventKinds.Rolled)
                if (Enabled(rules, kind)) total += rules.Chance(kind);
            if (!(total > 0)) return null;
            var scale = total > 1 ? 1 / total : 1;
            double edge = 0;
            foreach (var kind in EventKinds.Rolled)
            {
                if (!Enabled(rules, kind)) continue;
                edge += rules.Chance(kind) * scale;
                if (roll < edge) return kind;
            }
            return null;
        }

        static bool Enabled(ScheduleRules rules, EventKind kind) => rules.Chance(kind) > 0 && rules.Biomes(kind) != WeatherBiome.None;

        /// <summary>
        /// Weather command "summon" (design 5): starts now regardless of chance, gap and players; <paramref name="skipWarning"/>
        /// is the "now" option and <paramref name="dry"/> the "dry" option (null rolls Lightning Storm Chance as a scheduled
        /// storm does; kind LightningStorm is always dry). A Meteor Shower without "now" waits for the night as a scheduled one
        /// does (its warning lasts until nightfall); with "now" it starts at once, day or night. Refused while an event runs
        /// (Busy), when the kind has no biome configured, or when no night is in reach (Refused).
        /// </summary>
        public static SchedulerResult Summon(ScheduleState state, ScheduleRules rules, EventKind kind, double now, bool skipWarning, int seed, double dayLength,
            bool? dry = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (state.Current != null && !Timeline.Finished(state.Current, now)) return new SchedulerResult(state, SchedulerOutcome.Busy);
            if (rules.Biomes(kind) == WeatherBiome.None || state.NextSerial >= ScheduleState.MaximumSerial) return new SchedulerResult(state, SchedulerOutcome.Refused);
            var length = rules.ActiveMinSeconds + (int)Math.Floor(SeededRandom.Unit((ulong)seed, (ulong)now, LengthSalt) * (rules.ActiveMaxSeconds - rules.ActiveMinSeconds + 1));
            var storm = kind == EventKind.Thunderstorm || kind == EventKind.LightningStorm;
            var isDry = kind == EventKind.LightningStorm ? true : dry.HasValue ? dry.Value && storm : (bool?)null;
            return Start(state, rules, storm ? EventKind.Thunderstorm : kind, now, skipWarning ? 0 : rules.WarningSeconds, Math.Min(length, rules.ActiveMaxSeconds),
                seed, dayLength, isDry, nightRule: !skipWarning);
        }

        /// <summary>The kind a summon actually started (a storm may roll Lightning Storm Chance), or <paramref name="requested"/> when nothing started.</summary>
        public static EventKind SummonedKind(SchedulerResult result, EventKind requested) =>
            result.Outcome == SchedulerOutcome.Started && result.State.Current != null ? result.State.Current.Kind : requested;

        /// <summary>Weather command "stop": the current event goes straight to Clearing from now.</summary>
        public static SchedulerResult Stop(ScheduleState state, double now)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var e = state.Current;
            if (e == null || Timeline.Finished(e, now)) return new SchedulerResult(state.With(null), SchedulerOutcome.None);
            var start = Math.Max(0, now);
            return new SchedulerResult(state.With(new TempestEvent(e.Serial, e.Kind, start, 0, 0, e.ClearingSeconds, e.Seed, e.Biomes)), SchedulerOutcome.Stopped);
        }

        /// <summary>
        /// Whether a new event is a dry thunderstorm: a roll on the event's own seed and serial against
        /// <paramref name="chance"/>. The authority rolls once when the event starts and the result travels in the event
        /// record, so every peer and every reload sees the same answer even if the setting changes later.
        /// </summary>
        public static bool RollDry(EventKind kind, int seed, int serial, float chance) =>
            kind == EventKind.Thunderstorm && chance > 0 && SeededRandom.Unit(unchecked((ulong)seed), (ulong)serial, DrySalt) < chance;

        static SchedulerResult Start(ScheduleState state, ScheduleRules rules, EventKind kind, double now, int warning, int active, int seed, double dayLength, bool? dry,
            bool nightRule)
        {
            if (nightRule && EventKinds.NightOnly(kind)
                && !NightWindow.Fit(Math.Max(0, now), dayLength, warning, active, rules.ActiveMinSeconds, out warning, out active))
                return new SchedulerResult(state, SchedulerOutcome.Refused);
            var serial = state.NextSerial;
            var isDry = dry ?? RollDry(kind, seed, serial, rules.DryStormChance);
            var e = new TempestEvent(serial, kind, Math.Max(0, now), warning, active, rules.ClearingSeconds, seed, rules.Biomes(kind), isDry);
            return new SchedulerResult(new ScheduleState(e, state.LastRolledDay, DayOf(now, dayLength), e.Serial + 1), SchedulerOutcome.Started);
        }
    }

    /// <summary>
    /// Night in world time, as the game draws it: <c>EnvMan</c> rescales the raw day fraction so that raw 0.15 (morning
    /// start) becomes 0.25 and raw 0.85 becomes 0.75, and <c>EnvMan.IsNight</c> is true below 0.25 or above 0.75. In raw
    /// fractions of <see cref="Scheduler.DayOf"/> the night is therefore [0.85, 1) and [0, 0.15).
    /// </summary>
    public static class NightWindow
    {
        public const double Dusk = 0.85;
        public const double Dawn = 0.15;

        public static double Length(double dayLength) => (1 - Dusk + Dawn) * dayLength;

        public static bool IsNight(double now, double dayLength)
        {
            if (!(dayLength > 0) || now < 0) return false;
            var f = now / dayLength - Math.Floor(now / dayLength);
            return f >= Dusk || f < Dawn;
        }

        /// <summary>The first night moment at or after <paramref name="time"/> and the dawn that ends that night.</summary>
        public static (double Start, double End) Next(double time, double dayLength)
        {
            var day = Math.Floor(time / dayLength);
            var f = time / dayLength - day;
            if (f < Dawn) return (time, (day + Dawn) * dayLength);
            if (f >= Dusk) return (time, (day + 1 + Dawn) * dayLength);
            return ((day + Dusk) * dayLength, (day + 1 + Dawn) * dayLength);
        }

        /// <summary>
        /// Fits an Active phase into the night: when the planned Active start (<paramref name="now"/> plus the warning) is at
        /// night with at least Active Min Seconds (or the whole night, if shorter) before dawn, the warning stays and the Active
        /// phase is cut at dawn; otherwise the warning lasts until the next dusk and the Active phase is at most one night. False
        /// when that warning would pass <see cref="TempestEvent.MaximumPhaseSeconds"/> or the day length is unusable.
        /// </summary>
        public static bool Fit(double now, double dayLength, int warning, int active, int activeMin, out int fitWarning, out int fitActive)
        {
            fitWarning = warning;
            fitActive = active;
            if (!(dayLength > 0) || double.IsInfinity(dayLength) || active < 1) return false;
            var night = Length(dayLength);
            var need = Math.Max(1, Math.Min(activeMin, (int)Math.Floor(night)));
            var start = now + warning;
            var window = Next(start, dayLength);
            if (window.Start > start || window.End - start < need)
            {
                window = Next(window.Start > start ? window.Start : window.End, dayLength);
                fitWarning = (int)Math.Ceiling(window.Start - now);
                start = now + fitWarning;
            }
            if (fitWarning > TempestEvent.MaximumPhaseSeconds) return false;
            fitActive = (int)Math.Max(1, Math.Min(active, Math.Floor(window.End - start)));
            return true;
        }
    }

    /// <summary>SplitMix64 over three inputs: the same inputs give the same numbers on every peer and platform.</summary>
    public static class SeededRandom
    {
        public static ulong Next(ulong a, ulong b, ulong c)
        {
            var x = Mix(Mix(Mix(a ^ 0x9E3779B97F4A7C15UL) ^ b) ^ c);
            return x;
        }

        /// <summary>A number in [0, 1).</summary>
        public static double Unit(ulong a, ulong b, ulong c) => (Next(a, b, c) >> 11) * (1.0 / 9007199254740992.0);

        static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}

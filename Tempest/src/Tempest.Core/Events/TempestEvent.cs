// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the shared event of design 1.2 (WildWeather's blizzard, thunderstorm and warning, behaviour only).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Tempest.Events
{
    /// <summary>
    /// The event kinds. The numbers are stored in saves and sent between peers, so new kinds are only ever appended.
    /// Blizzard and Thunderstorm travel in the main record; a Lightning Storm travels there as a thunderstorm plus the
    /// lightning storm mark (<see cref="DryMark"/>); the later kinds travel only in the extended record
    /// (<see cref="ExtendedRecord"/>), so older peers see a thunderstorm or nothing.
    /// </summary>
    public enum EventKind { Blizzard, Thunderstorm, LightningStorm, DenseFog, AshStorm, MeteorShower }

    /// <summary>Per-kind facts: name token, strikes, night-only rule and how the kind travels to older peers.</summary>
    public static class EventKinds
    {
        public static IReadOnlyList<EventKind> All { get; } = Array.AsReadOnly((EventKind[])Enum.GetValues(typeof(EventKind)));

        /// <summary>The kinds the daily roll picks from (a Lightning Storm is a thunderstorm that rolled Lightning Storm Chance).</summary>
        public static IReadOnlyList<EventKind> Rolled { get; } = Array.AsReadOnly(new[]
            { EventKind.Blizzard, EventKind.Thunderstorm, EventKind.DenseFog, EventKind.AshStorm, EventKind.MeteorShower });

        /// <summary>Lowercase name token for text keys and the detail contract: blizzard, thunderstorm, lightning_storm, dense_fog, ash_storm, meteor_shower.</summary>
        public static string Token(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Blizzard: return "blizzard";
                case EventKind.Thunderstorm: return "thunderstorm";
                case EventKind.LightningStorm: return "lightning_storm";
                case EventKind.DenseFog: return "dense_fog";
                case EventKind.AshStorm: return "ash_storm";
                case EventKind.MeteorShower: return "meteor_shower";
                default: throw new ArgumentException("Unknown kind.", nameof(kind));
            }
        }

        /// <summary>Kinds whose Active phase lands strikes (lightning, or meteors for a Meteor Shower).</summary>
        public static bool Strikes(EventKind kind) => kind == EventKind.Thunderstorm || kind == EventKind.LightningStorm || kind == EventKind.MeteorShower;

        /// <summary>Kinds whose strikes are lightning (with lingering ground lightning).</summary>
        public static bool Lightning(EventKind kind) => kind == EventKind.Thunderstorm || kind == EventKind.LightningStorm;

        /// <summary>A Meteor Shower is scheduled so that its Active phase falls at night (<see cref="NightWindow"/>).</summary>
        public static bool NightOnly(EventKind kind) => kind == EventKind.MeteorShower;

        /// <summary>Kinds that older peers cannot read; they travel only in the extended record.</summary>
        public static bool Extended(EventKind kind) => kind == EventKind.DenseFog || kind == EventKind.AshStorm || kind == EventKind.MeteorShower;
    }

    /// <summary>Idle: no event (before its start or after its end). Then Warning, Active, Clearing in that order.</summary>
    public enum EventPhase { Idle, Warning, Active, Clearing }

    /// <summary>Tempest's mirror of the game's <c>Heightmap.Biome</c> flags (same values).</summary>
    [Flags]
    public enum WeatherBiome
    {
        None = 0,
        Meadows = 1,
        Swamp = 2,
        Mountain = 4,
        BlackForest = 8,
        Plains = 16,
        AshLands = 32,
        DeepNorth = 64,
        Ocean = 256,
        Mistlands = 512,
    }

    /// <summary>Which biomes each kind may cover (design 4.1, W10) and the text form of a biome set.</summary>
    public static class BiomeSets
    {
        static readonly WeatherBiome[] Order =
        {
            WeatherBiome.Meadows, WeatherBiome.BlackForest, WeatherBiome.Swamp, WeatherBiome.Mountain, WeatherBiome.Plains,
            WeatherBiome.Ocean, WeatherBiome.Mistlands, WeatherBiome.AshLands, WeatherBiome.DeepNorth,
        };

        const WeatherBiome Lowlands = WeatherBiome.Meadows | WeatherBiome.BlackForest | WeatherBiome.Swamp | WeatherBiome.Plains;

        public static WeatherBiome Allowed(EventKind kind)
        {
            switch (kind)
            {
                // Deep North: a whiteout (EnvironmentPlan.Whiteout); Mountain and Mistlands stay excluded for every kind.
                case EventKind.Blizzard: return Lowlands | WeatherBiome.DeepNorth;
                case EventKind.AshStorm: return WeatherBiome.AshLands;
                case EventKind.MeteorShower: return WeatherBiome.Plains | WeatherBiome.AshLands;
                default: return Lowlands | WeatherBiome.Ocean;
            }
        }

        public static WeatherBiome Default(EventKind kind) =>
            kind == EventKind.Blizzard ? WeatherBiome.Meadows | WeatherBiome.BlackForest | WeatherBiome.DeepNorth : Allowed(kind);

        /// <summary>Names separated by commas, any case; unknown names and biomes the kind does not allow are dropped.</summary>
        public static WeatherBiome Parse(EventKind kind, string? text)
        {
            var result = WeatherBiome.None;
            foreach (var part in (text ?? "").Split(','))
            {
                var name = part.Trim();
                if (name.Length == 0) continue;
                foreach (var biome in Order)
                    if (string.Equals(biome.ToString(), name, StringComparison.OrdinalIgnoreCase)) result |= biome;
            }
            return result & Allowed(kind);
        }

        public static string Format(WeatherBiome biomes) => string.Join(", ", Order.Where(b => (biomes & b) != 0).Select(b => b.ToString()));

        public static string Normalize(EventKind kind, string? text) => Format(Parse(kind, text));

        /// <summary>The names in <paramref name="text"/> that <see cref="Parse"/> drops: unknown names and biomes the kind does not allow.</summary>
        public static IReadOnlyList<string> Dropped(EventKind kind, string? text)
        {
            var dropped = new List<string>();
            var allowed = Allowed(kind);
            foreach (var part in (text ?? "").Split(','))
            {
                var name = part.Trim();
                if (name.Length == 0) continue;
                if (!Order.Any(b => (allowed & b) != 0 && string.Equals(b.ToString(), name, StringComparison.OrdinalIgnoreCase))) dropped.Add(name);
            }
            return dropped;
        }

        /// <summary>The single biome flag a player stands in, or None for zero or several flags.</summary>
        public static bool IsSingle(WeatherBiome biome) => biome != WeatherBiome.None && ((int)biome & ((int)biome - 1)) == 0;

        public static IEnumerable<WeatherBiome> All => Order;
    }

    /// <summary>
    /// One shared event (design 1.2). Times are world seconds (the game's ZNet time): Warning from <see cref="Start"/>,
    /// then Active, then Clearing; a phase of zero seconds is skipped (summon "now" has no warning).
    /// </summary>
    public sealed class TempestEvent : IEquatable<TempestEvent>
    {
        /// <summary>Upper bound of any one phase (design 4.1: Active Max Seconds reaches 3600).</summary>
        public const int MaximumPhaseSeconds = 3600;

        public TempestEvent(int serial, EventKind kind, double start, int warningSeconds, int activeSeconds, int clearingSeconds, int seed, WeatherBiome biomes,
            bool dry = false)
        {
            if (serial < 1) throw new ArgumentException("Serial starts at 1.", nameof(serial));
            if (!Enum.IsDefined(typeof(EventKind), kind)) throw new ArgumentException("Unknown kind.", nameof(kind));
            if (double.IsNaN(start) || double.IsInfinity(start) || start < 0) throw new ArgumentException("Expected a finite world time.", nameof(start));
            Check(warningSeconds, nameof(warningSeconds));
            Check(activeSeconds, nameof(activeSeconds));
            Check(clearingSeconds, nameof(clearingSeconds));
            if (biomes == WeatherBiome.None || (biomes & ~BiomeSets.Allowed(kind)) != 0)
                throw new ArgumentException("Biomes must be a non-empty set this kind allows.", nameof(biomes));
            if (dry && kind != EventKind.Thunderstorm && kind != EventKind.LightningStorm) throw new ArgumentException("Only a thunderstorm can be dry.", nameof(dry));
            Serial = serial;
            // A dry thunderstorm is a Lightning Storm (the flag is kept for callers and older records).
            Kind = dry ? EventKind.LightningStorm : kind;
            Start = start;
            WarningSeconds = warningSeconds;
            ActiveSeconds = activeSeconds;
            ClearingSeconds = clearingSeconds;
            Seed = seed;
            Biomes = biomes;
        }

        public int Serial { get; }
        public EventKind Kind { get; }
        public double Start { get; }
        public int WarningSeconds { get; }
        public int ActiveSeconds { get; }
        public int ClearingSeconds { get; }

        /// <summary>Seed for lightning plans (design 1.2): every peer derives the same strikes from it.</summary>
        public int Seed { get; }

        public WeatherBiome Biomes { get; }

        /// <summary>A Lightning Storm: the storm's lightning, wind and darkness without rain, so strikes can start fires.</summary>
        public bool Dry => Kind == EventKind.LightningStorm;

        public double ActiveStart => Start + WarningSeconds;
        public double ClearingStart => ActiveStart + ActiveSeconds;
        public double End => ClearingStart + ClearingSeconds;

        /// <summary>A player standing in <paramref name="biome"/> gets this event's weather (design 1.2 scope).</summary>
        public bool Affects(WeatherBiome biome) => biome != WeatherBiome.None && (Biomes & biome) == biome;

        public bool Equals(TempestEvent? other) => other != null && Serial == other.Serial && Kind == other.Kind && Start.Equals(other.Start)
            && WarningSeconds == other.WarningSeconds && ActiveSeconds == other.ActiveSeconds && ClearingSeconds == other.ClearingSeconds
            && Seed == other.Seed && Biomes == other.Biomes;

        public override bool Equals(object? obj) => obj is TempestEvent other && Equals(other);

        public override int GetHashCode() => ((Serial * 397 ^ (int)Kind) * 397 ^ Start.GetHashCode()) * 397 ^ Seed;

        public override string ToString() => Kind + " #" + Serial + " at " + Start.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
            + " (" + WarningSeconds + "/" + ActiveSeconds + "/" + ClearingSeconds + " s, " + BiomeSets.Format(Biomes) + ")";

        static void Check(int seconds, string name)
        {
            if (seconds < 0 || seconds > MaximumPhaseSeconds) throw new ArgumentException("Phase length out of range.", name);
        }
    }

    /// <summary>Where an event stands at one world time.</summary>
    public readonly struct PhaseState
    {
        public PhaseState(EventPhase phase, double secondsLeft, double progress, double secondsUntilActive)
        {
            Phase = phase;
            SecondsLeft = secondsLeft;
            Progress = progress;
            SecondsUntilActive = secondsUntilActive;
        }

        public static PhaseState Idle => new PhaseState(EventPhase.Idle, 0, 0, 0);

        public EventPhase Phase { get; }

        /// <summary>Seconds until the current phase ends.</summary>
        public double SecondsLeft { get; }

        /// <summary>0 at the phase start, towards 1 at its end.</summary>
        public double Progress { get; }

        /// <summary>Seconds until Active begins (0 once it has).</summary>
        public double SecondsUntilActive { get; }
    }

    /// <summary>Phase math in world time (design 1.2). Sleeping skips world time, so a sleep through an event ends it.</summary>
    public static class Timeline
    {
        public static PhaseState At(TempestEvent e, double now)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (double.IsNaN(now) || now < e.Start || now >= e.End) return PhaseState.Idle;
            if (now < e.ActiveStart) return Make(EventPhase.Warning, now, e.Start, e.WarningSeconds, e.ActiveStart - now);
            if (now < e.ClearingStart) return Make(EventPhase.Active, now, e.ActiveStart, e.ActiveSeconds, 0);
            return Make(EventPhase.Clearing, now, e.ClearingStart, e.ClearingSeconds, 0);
        }

        public static bool Finished(TempestEvent e, double now) => e != null && now >= e.End;

        static PhaseState Make(EventPhase phase, double now, double from, int length, double untilActive)
        {
            var elapsed = now - from;
            return new PhaseState(phase, length - elapsed, length <= 0 ? 0 : elapsed / length, untilActive);
        }
    }
}

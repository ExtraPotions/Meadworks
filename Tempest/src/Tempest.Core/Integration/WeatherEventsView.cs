// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Tempest's reading for the Crucible capability meadworks.weather.events 1.0 (design 8, TP07).
using System;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Integration
{
    /// <summary>
    /// One reading in the numbering of Crucible's <c>WeatherEventsContract</c> 1.0 (meadworks-crucible commit cda9696):
    /// kind (0 none, 1 blizzard, 2 thunderstorm), phase (0 idle, 1 warning, 2 active, 3 clearing), seconds left in the
    /// phase (0 to 3600), eligible biomes as <c>Heightmap.Biome</c> bits, whether the local player's biome gets the event
    /// now, and a revision bumped when kind, phase or biomes change. Tempest.Core does not reference the contract assembly;
    /// the plugin packs these fields into the contract's BCL tuple. A Lightning Storm reads as a thunderstorm (2); the kinds
    /// 1.0 cannot name (<see cref="EventKinds.Extended"/>) read as idle, so an older consumer shows nothing for them.
    /// </summary>
    public readonly struct WeatherEventsView : IEquatable<WeatherEventsView>
    {
        public const string Id = "meadworks.weather.events";
        public static readonly Version ContractVersion = new Version(1, 0);
        public const float MaximumSecondsLeft = 3600f;

        public WeatherEventsView(int kind, int phase, float secondsLeft, int biomes, bool localAffected, int revision)
        {
            Kind = kind;
            Phase = phase;
            SecondsLeft = secondsLeft;
            Biomes = biomes;
            LocalAffected = localAffected;
            Revision = revision;
        }

        public int Kind { get; }
        public int Phase { get; }
        public float SecondsLeft { get; }
        public int Biomes { get; }
        public bool LocalAffected { get; }
        public int Revision { get; }

        public WeatherEventsView WithRevision(int revision) => new WeatherEventsView(Kind, Phase, SecondsLeft, Biomes, LocalAffected, revision);

        /// <summary>The reading at <paramref name="now"/> for a local player in <paramref name="localBiome"/> (revision 0).</summary>
        public static WeatherEventsView Read(TempestEvent? e, double now, WeatherBiome localBiome, bool gameForced)
        {
            if (e == null) return default;
            var state = Timeline.At(e, now);
            if (state.Phase == EventPhase.Idle) return default;
            var seconds = (float)Math.Max(0, Math.Min(MaximumSecondsLeft, state.SecondsLeft));
            if (EventKinds.Extended(e.Kind)) return default;
            return new WeatherEventsView(e.Kind == EventKind.Blizzard ? 1 : 2, (int)state.Phase, seconds, (int)e.Biomes,
                !gameForced && e.Affects(localBiome), 0);
        }

        /// <summary>
        /// The reading for <see cref="WeatherEventsDetail"/>: the same fields with the open kind number
        /// (<see cref="WeatherEventsDetail.Number"/>) for every kind, and the kind's name token (empty while idle).
        /// </summary>
        public static (WeatherEventsView View, string Token) ReadDetail(TempestEvent? e, double now, WeatherBiome localBiome, bool gameForced)
        {
            if (e == null) return (default, "");
            var state = Timeline.At(e, now);
            if (state.Phase == EventPhase.Idle) return (default, "");
            var seconds = (float)Math.Max(0, Math.Min(MaximumSecondsLeft, state.SecondsLeft));
            return (new WeatherEventsView(WeatherEventsDetail.Number(e.Kind), (int)state.Phase, seconds, (int)e.Biomes, !gameForced && e.Affects(localBiome), 0),
                EventKinds.Token(e.Kind));
        }

        public bool Equals(WeatherEventsView other) => Kind == other.Kind && Phase == other.Phase && SecondsLeft.Equals(other.SecondsLeft)
            && Biomes == other.Biomes && LocalAffected == other.LocalAffected && Revision == other.Revision;

        public override bool Equals(object? obj) => obj is WeatherEventsView other && Equals(other);

        public override int GetHashCode() => (((Kind * 397 ^ Phase) * 397 ^ Biomes) * 397 ^ Revision) ^ (LocalAffected ? 7919 : 0);
    }

    /// <summary>
    /// Capability <c>meadworks.weather.events.detail</c> 1.0 (Crucible <c>WeatherEventsDetailContract</c>), next to 1.0 so
    /// older consumers keep working: the shape <c>Func&lt;Tuple&lt;int,int,float,int,bool,int,string&gt;&gt;</c> adds a name
    /// token to the 1.0 items, and the kind number is open (consumers must accept numbers they do not know): 1 Blizzard,
    /// 2 Thunderstorm, 3 Lightning Storm, 4 Dense Fog, 5 Ash Storm, 6 Meteor Shower. Tokens are lowercase letters, digits and
    /// underscores (<see cref="EventKinds.Token"/>).
    /// </summary>
    public static class WeatherEventsDetail
    {
        public const string Id = "meadworks.weather.events.detail";
        public static readonly Version ContractVersion = new Version(1, 0);

        /// <summary>The open kind number: the kind's position plus 1 (Blizzard and Thunderstorm keep their 1.0 numbers).</summary>
        public static int Number(EventKind kind) => (int)kind + 1;
    }

    /// <summary>The contract's change counter: bumps when kind, phase or biomes differ from the last reading.</summary>
    public sealed class RevisionCounter
    {
        int revision;
        int kind = -1;
        int phase = -1;
        int biomes = -1;

        public int Observe(WeatherEventsView view)
        {
            if (view.Kind != kind || view.Phase != phase || view.Biomes != biomes)
            {
                kind = view.Kind;
                phase = view.Phase;
                biomes = view.Biomes;
                revision = unchecked(revision + 1);
            }
            return revision;
        }
    }
}

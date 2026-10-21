// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server-decided lightning strike orders (design 1.2, 4.2, W2, W3, W10, Q2; WildWeather behaviour only).
using System;
using System.Collections.Generic;
using System.Globalization;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Weather
{
    /// <summary>What one strike may do, decided by the authority from its settings (design 4.2).</summary>
    [Flags]
    public enum StrikeEffects
    {
        None = 0,
        HurtsPlayers = 1,
        StartsFires = 2,
        HitsTrees = 4,
    }

    /// <summary>
    /// One strike decided by the authority: the event serial, the player it is planned around (peer id), its world time,
    /// the ground point (x, z), its lightning damage and effects. Sent to that player's client, which executes it with the
    /// game's own lightning object (the game simulates damage on the owner) so other players see it.
    /// </summary>
    public readonly struct StrikeOrder : IEquatable<StrikeOrder>
    {
        public const string Prefix = "s1";
        public const int MaximumLength = 256;
        public const float MaximumCoordinate = 100000;
        public const float MaximumDamage = 200;
        const double MaximumTime = 1e12;
        const int FieldCount = 8;

        public StrikeOrder(int serial, long playerId, double time, float x, float z, float damage, StrikeEffects effects)
        {
            Serial = serial;
            PlayerId = playerId;
            Time = time;
            X = x;
            Z = z;
            Damage = damage;
            Effects = effects;
        }

        public int Serial { get; }
        public long PlayerId { get; }
        public double Time { get; }
        public float X { get; }
        public float Z { get; }
        public float Damage { get; }
        public StrikeEffects Effects { get; }

        /// <summary>Identity for duplicate suppression: event, player and planned time.</summary>
        public string Id => Serial.ToString(CultureInfo.InvariantCulture) + ":" + PlayerId.ToString(CultureInfo.InvariantCulture) + ":" + Num(Time);

        public string Encode() => string.Join(";", Prefix, Serial.ToString(CultureInfo.InvariantCulture), PlayerId.ToString(CultureInfo.InvariantCulture),
            Num(Time), Num(X), Num(Z), Num(Damage), ((int)Effects).ToString(CultureInfo.InvariantCulture));

        public static bool TryDecode(string? text, out StrikeOrder order)
        {
            order = default;
            if (string.IsNullOrEmpty(text) || text!.Length > MaximumLength) return false;
            var f = text.Split(';');
            if (f.Length != FieldCount || f[0] != Prefix) return false;
            const NumberStyles Int = NumberStyles.AllowLeadingSign;
            const NumberStyles Real = NumberStyles.Float;
            var c = CultureInfo.InvariantCulture;
            if (!int.TryParse(f[1], Int, c, out var serial) || serial <= 0) return false;
            if (!long.TryParse(f[2], Int, c, out var player) || player == 0) return false;
            if (!double.TryParse(f[3], Real, c, out var time) || !Finite(time) || time < 0 || time > MaximumTime) return false;
            if (!float.TryParse(f[4], Real, c, out var x) || !Finite(x) || Math.Abs(x) > MaximumCoordinate) return false;
            if (!float.TryParse(f[5], Real, c, out var z) || !Finite(z) || Math.Abs(z) > MaximumCoordinate) return false;
            if (!float.TryParse(f[6], Real, c, out var damage) || !Finite(damage) || damage < 0 || damage > MaximumDamage) return false;
            if (!int.TryParse(f[7], Int, c, out var effects) || effects < 0 || effects > 7) return false;
            order = new StrikeOrder(serial, player, time, x, z, damage, (StrikeEffects)effects);
            return true;
        }

        public bool Equals(StrikeOrder other) => Serial == other.Serial && PlayerId == other.PlayerId && Time.Equals(other.Time)
            && X.Equals(other.X) && Z.Equals(other.Z) && Damage.Equals(other.Damage) && Effects == other.Effects;

        public override bool Equals(object? obj) => obj is StrikeOrder other && Equals(other);

        public override int GetHashCode() => ((Serial * 397 ^ PlayerId.GetHashCode()) * 397 ^ Time.GetHashCode()) * 397 ^ (int)Effects;

        public override string ToString() => Encode();

        static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();

        static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture).ToLowerInvariant();
    }

    /// <summary>Why a client executes or drops a strike order.</summary>
    public enum StrikeVerdict { Accepted, OtherPlayer, NoEvent, OtherEvent, NotActive, Stale, Forced }

    /// <summary>The authority's plan in world coordinates and the client's acceptance rule.</summary>
    public static class StrikeOrders
    {
        /// <summary>Seconds a client accepts an order before or after its planned time (late delivery, clock drift).</summary>
        public const double AcceptWindowSeconds = 10;

        /// <summary>Seconds between the telegraph flash and the strike on the executing client.</summary>
        public const double TelegraphSeconds = 1;

        /// <summary>
        /// Orders for one player with planned times in (<paramref name="from"/>, <paramref name="to"/>]. Points for which
        /// <paramref name="spared"/> answers true (inside a base, ward or piece clearance, W3) are dropped.
        /// </summary>
        public static IReadOnlyList<StrikeOrder> Plan(TempestEvent e, long playerId, float playerX, float playerZ, int perMinute, float damage,
            StrikeEffects effects, double from, double to, Func<float, float, bool> spared)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (spared == null) throw new ArgumentNullException(nameof(spared));
            var dmg = float.IsNaN(damage) ? 0 : Math.Max(0, Math.Min(StrikeOrder.MaximumDamage, damage));
            var list = new List<StrikeOrder>();
            foreach (var s in StrikePlanner.Due(e, playerId, perMinute, from, to))
            {
                var x = playerX + s.X;
                var z = playerZ + s.Z;
                if (Math.Abs(x) > StrikeOrder.MaximumCoordinate || Math.Abs(z) > StrikeOrder.MaximumCoordinate || spared(x, z)) continue;
                list.Add(new StrikeOrder(e.Serial, playerId, s.Time, x, z, dmg, effects));
            }
            return list;
        }

        /// <summary>The client executes only orders for its own player, for the current storm's Active phase, on time, outside forced environments and interiors (W10).</summary>
        public static StrikeVerdict Accept(StrikeOrder order, TempestEvent? current, long localPlayerId, double now, bool forced)
        {
            if (order.PlayerId != localPlayerId) return StrikeVerdict.OtherPlayer;
            if (current == null) return StrikeVerdict.NoEvent;
            if (current.Serial != order.Serial) return StrikeVerdict.OtherEvent;
            if (!EventKinds.Strikes(current.Kind) || order.Time < current.ActiveStart - 1 || order.Time >= current.ClearingStart) return StrikeVerdict.NotActive;
            if (Math.Abs(now - order.Time) > AcceptWindowSeconds) return StrikeVerdict.Stale;
            if (now < current.ActiveStart || now >= current.ClearingStart) return StrikeVerdict.NotActive;
            if (forced) return StrikeVerdict.Forced;
            return StrikeVerdict.Accepted;
        }
    }

    /// <summary>Bounded first-in first-out set of executed order ids, so a resent order strikes once.</summary>
    public sealed class StrikeLedger
    {
        readonly int capacity;
        readonly Queue<string> order = new Queue<string>();
        readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

        public StrikeLedger(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public int Count => seen.Count;

        public bool Admit(string id)
        {
            if (id == null || seen.Contains(id)) return false;
            if (order.Count >= capacity) seen.Remove(order.Dequeue());
            order.Enqueue(id);
            seen.Add(id);
            return true;
        }

        public void Clear()
        {
            order.Clear();
            seen.Clear();
        }
    }

    /// <summary>What a strike touched, as the executing client classifies it.</summary>
    public enum StrikeTarget { Player, Creature, Tree, Piece, Other }

    /// <summary>Q2 and design 4.2: players only while Strike Hurts Players is on, trees only while Strike Hits Trees is on, player-built pieces never (W3).</summary>
    public static class StrikeTargets
    {
        public static bool Allow(StrikeEffects effects, StrikeTarget target)
        {
            switch (target)
            {
                case StrikeTarget.Player: return (effects & StrikeEffects.HurtsPlayers) != 0;
                case StrikeTarget.Tree: return (effects & StrikeEffects.HitsTrees) != 0;
                case StrikeTarget.Piece: return false;
                default: return true;
            }
        }

        /// <summary>Rain cancels fire: while the game reports rain (EnvMan.IsWet) a strike carries no fire at all.</summary>
        public static StrikeEffects InWeather(StrikeEffects effects, bool raining) =>
            raining ? effects & ~StrikeEffects.StartsFires : effects;

        /// <summary>
        /// Strike Starts Fires turns half of the strike's damage into fire damage, so the total stays Strike Damage. The first
        /// part is lightning for a lightning strike and blunt for a meteor impact (the plugin picks the damage type).
        /// </summary>
        public static (float Lightning, float Fire) Damage(float damage, StrikeEffects effects)
        {
            if (float.IsNaN(damage) || damage <= 0) return (0, 0);
            if ((effects & StrikeEffects.StartsFires) == 0) return (damage, 0);
            return (damage / 2, damage / 2);
        }

        /// <summary>
        /// The strike may set this target alight with the game's fire: Strike Starts Fires on, trees only with Strike Hits
        /// Trees, never a player-built piece or a character, and never inside an active ward or shield. The game's own
        /// burn check (wet, under water, biome) still decides afterwards.
        /// </summary>
        public static bool CanIgnite(StrikeEffects effects, StrikeTarget target, bool insideWard, bool insideShield)
        {
            if ((effects & StrikeEffects.StartsFires) == 0 || insideWard || insideShield) return false;
            switch (target)
            {
                case StrikeTarget.Tree: return (effects & StrikeEffects.HitsTrees) != 0;
                case StrikeTarget.Other: return true;
                default: return false;
            }
        }

        public static StrikeEffects Effects(bool hurtsPlayers, bool startsFires, bool hitsTrees) =>
            (hurtsPlayers ? StrikeEffects.HurtsPlayers : StrikeEffects.None)
            | (startsFires ? StrikeEffects.StartsFires : StrikeEffects.None)
            | (hitsTrees ? StrikeEffects.HitsTrees : StrikeEffects.None);
    }
}

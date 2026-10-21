// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The short-lived states of the local player's affixes: Momentum (5 s after a dodge), Evader's Fury (10 s after
    /// dodging through a melee hit, no refresh while active), Steel Rhythm (2 s after a combo finisher), the Runic Ward
    /// (charged after 10 s without damage) and Mending's 10 s heal. Started by the hooks, advanced once per frame by
    /// <see cref="EffectRuntime.Tick"/> (never from inside a status-effect update, which may not add status effects).
    /// Local player only: the states describe the one player this client owns; nothing is sent.
    /// </summary>
    internal static class CombatWindows
    {
        /// <summary>Judgement calls from affixes.md: window lengths and Mending's interval.</summary>
        public const float MomentumSeconds = 5f, FurySeconds = 10f, RhythmSeconds = 2f, WardCalmSeconds = 10f, MendSeconds = 10f;

        private static float _momentumUntil, _furyUntil, _rhythmUntil, _lastDamage, _nextMend;
        private static bool _furyShown, _rhythmShown, _wardShown;

        public static bool MomentumActive => Time.time < _momentumUntil;
        public static bool FuryActive => Time.time < _furyUntil;
        public static bool RhythmActive => Time.time < _rhythmUntil;

        /// <summary>The ward's remaining absorption; 0 when not charged.</summary>
        public static float WardPool { get; private set; }

        public static void Reset()
        {
            _momentumUntil = _furyUntil = _rhythmUntil = 0f;
            _lastDamage = Time.time;
            _nextMend = Time.time + MendSeconds;
            WardPool = 0f;
            _furyShown = _rhythmShown = _wardShown = false;
        }

        /// <summary>Player.Dodge on the local player.</summary>
        public static void OnDodge()
        {
            if (AggregateHost.Current[EffectKind.MoveSpeedAfterDodge] > 0f)
            {
                _momentumUntil = Time.time + MomentumSeconds;
            }
        }

        /// <summary>A melee hit passed through the local player's dodge; does not refresh while active.</summary>
        public static void OnDodgedMelee()
        {
            if (!FuryActive && AggregateHost.Current[EffectKind.DodgeFury] > 0f)
            {
                _furyUntil = Time.time + FurySeconds;
            }
        }

        /// <summary>The local player landed the last hit of a combo chain.</summary>
        public static void OnComboFinisher()
        {
            if (AggregateHost.Current[EffectKind.ComboFinisher] > 0f)
            {
                _rhythmUntil = Time.time + RhythmSeconds;
            }
        }

        /// <summary>
        /// Incoming damage on the local player, after armor: the ward takes what it can, and any hit restarts the calm
        /// timer. Returns the damage left for the player. May run inside the player's status-effect update (a burning
        /// or poison tick), which must not remove a status effect: the spent ward's icon goes in the next <see cref="Tick"/>.
        /// </summary>
        public static float OnDamaged(float damage)
        {
            _lastDamage = Time.time;
            if (WardPool <= 0f || damage <= 0f)
            {
                return damage;
            }
            float absorbed = Mathf.Min(WardPool, damage);
            WardPool -= absorbed;
            if (WardPool <= 0.01f)
            {
                WardPool = 0f;
            }
            return damage - absorbed;
        }

        /// <summary>Once per frame on the local player's client.</summary>
        public static void Tick(Player player)
        {
            AggregateValues v = AggregateHost.Current;
            TickWard(v[EffectKind.CalmWard]);
            _wardShown = Show(Indicators.Ward, WardPool > 0f, _wardShown, Time.time);
            TickMending(player, v[EffectKind.HealthRecoveryFlat]);
            _furyShown = Show(Indicators.Fury, FuryActive, _furyShown, _furyUntil);
            _rhythmShown = Show(Indicators.Rhythm, RhythmActive, _rhythmShown, _rhythmUntil);
        }

        private static void TickWard(float size)
        {
            if (size <= 0f)
            {
                WardPool = 0f;
            }
            else if (WardPool <= 0f && Time.time - _lastDamage >= WardCalmSeconds)
            {
                WardPool = size;
            }
        }

        // Mending heals on its own timer, food or not; nothing when the player is full or dead.
        private static void TickMending(Player player, float heal)
        {
            if (Time.time < _nextMend)
            {
                return;
            }
            _nextMend = Time.time + MendSeconds;
            if (heal > 0f && !player.IsDead() && player.GetHealth() < player.GetMaxHealth())
            {
                player.Heal(heal);
            }
        }

        private static bool Show(int indicator, bool active, bool shown, float until)
        {
            if (active && !shown)
            {
                Indicators.Show(indicator, until - Time.time);
            }
            else if (!active && shown)
            {
                Indicators.Hide(indicator);
            }
            return active;
        }
    }
}

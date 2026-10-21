// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The struck player's half of a Brutal throw, judged on their own client - where the game applies a hit to a player,
    /// the only machine that may move their body, and the one place their dodge and block are known exactly. A hit
    /// marked by <see cref="BrutalBlow"/>, from a boss that carries Brutal, that lands on this machine's own player throws
    /// them away from the boss: `launch` metres across flat ground, peaking `lift` metres up, after the game has dealt the
    /// hit's damage. Three things stop it: a dodge roll's invulnerability (the hit never lands), a block that holds - the
    /// guard stays up, stamina left and not staggered through it, a parry included - and anything the game is holding
    /// the player in (seated, at a helm, riding, on a ship's deck, swimming, teleporting). A player already in the air
    /// from a throw is not thrown again. A broken guard, or a blow from behind, throws as any hit does.
    /// </summary>
    internal static class BrutalHit
    {
        /// <summary>The longest throw a rule file may ask for, in metres; 0 turns throwing off.</summary>
        private const float MaxLaunch = 40f;

        /// <summary>The lowest and highest peak a rule file may ask for, in metres: low enough to read as a shove along
        /// the ground, never so low that the ground catches the player straight away.</summary>
        private const float MinLift = 1f;
        private const float MaxLift = 15f;

        /// <summary>The fastest a throw ever carries a player across the ground, in m/s, so a long throw with a low peak
        /// cannot pass through a wall; such a throw falls short of `launch`.</summary>
        private const float MaxSpeed = 30f;

        private static HitData? _withstood;

        /// <summary>How far a throw carries across flat ground, in metres, from the live rules; 0 means no throw.</summary>
        public static float Launch() => Mathf.Clamp(AspectMath.Power(Aspect.Brutal, Fields.Launch), 0f, MaxLaunch);

        /// <summary>Before the game applies a hit: true when it is a marked hit on this machine's own player, not rolled
        /// through. The game drops a dodged hit without applying it, so this is read before it can.</summary>
        public static bool Aimed(Character victim, HitData hit) =>
            victim == Player.m_localPlayer && !(hit.m_dodgeable && victim.IsDodgeInvincible());

        /// <summary>
        /// The game has just let a block hold against a marked hit - it takes the blocked damage off the hit only then:
        /// stamina left and the guard not broken by the stagger, a parry included - so remember the hit. Read from the
        /// game's own decision, not re-derived after it: a parry without stamina regen spends its drain a second time
        /// afterwards, which would read as a broken guard.
        /// </summary>
        public static void Withstood(HitData hit) => _withstood = hit;

        /// <summary>After the game has applied a marked hit to this machine's own player: throw them, if it may.</summary>
        public static void Judge(Character victim, HitData hit)
        {
            bool withstood = ReferenceEquals(_withstood, hit);
            _withstood = null;
            BrutalBehaviour? brutal = Source(hit);
            if (withstood || brutal == null || !(victim is Player player) || !Free(player) || Launch() <= 0f)
            {
                return;
            }
            Vector3 from = player.transform.position;
            BrutalFlight.Launch(player, Velocity(Away(player, brutal.transform.position, hit)));
            brutal.Tell(from);
        }

        // The Brutal boss that dealt the hit, on this machine; null if it is gone or not Brutal here.
        private static BrutalBehaviour? Source(HitData hit)
        {
            Character attacker = hit.GetAttacker();
            return attacker != null ? attacker.GetComponent<BrutalBehaviour>() : null;
        }

        // Free to be thrown: alive, and not held by anything - a seat, bed, helm or saddle (attached), a ship's deck, the
        // water, a teleport or a cutscene - nor a ghost, debug-flying, or still in the air from the last throw.
        private static bool Free(Player player) =>
            player.GetHealth() > 0f && !player.IsDead() && !player.IsAttached() && player.GetStandingOnShip() == null
            && !player.IsSwimming() && !player.IsTeleporting() && !player.InCutscene() && !player.InGhostMode()
            && !player.IsDebugFlying() && !BrutalFlight.Flying(player);

        // Across the ground, away from the boss's body; the blow's own direction should the player stand inside it.
        private static Vector3 Away(Player player, Vector3 boss, HitData hit)
        {
            Vector3 away = player.transform.position - boss;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                away = new Vector3(hit.m_dir.x, 0f, hit.m_dir.z);
            }
            return away.sqrMagnitude > 0.0001f ? away.normalized : -player.transform.forward;
        }

        /// <summary>
        /// The take-off velocity for a throw of `launch` metres across flat ground peaking `lift` metres up, under the
        /// game's own gravity (20 m/s², twice Earth's): up fast enough to reach the peak, across fast enough to cover the
        /// distance in the time it takes to come back down, never faster than <see cref="MaxSpeed"/> across.
        /// </summary>
        private static Vector3 Velocity(Vector3 away)
        {
            float lift = Mathf.Clamp(AspectMath.Power(Aspect.Brutal, Fields.Lift), MinLift, MaxLift);
            float gravity = Mathf.Max(1f, -Physics.gravity.y);
            float up = Mathf.Sqrt(2f * gravity * lift);
            float across = Mathf.Min(Launch() * gravity / (2f * up), MaxSpeed); // distance over time in the air
            return away * across + Vector3.up * up;
        }
    }
}

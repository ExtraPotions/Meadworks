// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// A Brutal throw's flight, on this machine's own player, the only character this machine may move. The throw is the
    /// game's own forced jump (<c>Character.ForceJump</c>, without its jump animation and sound): it sets the body's
    /// velocity, lets go of the ground and ignores the ground for the game's 0.1 s jump guard, so the walking code treats
    /// the player as airborne from the first step. From then until they land, the game's walking intent is replaced by the
    /// body's own velocity where the game adds a knockback (<see cref="Patches.BrutalFlightPatch"/>), so neither the
    /// player's own air control, the stride of an attack nor the hit's ordinary shove eats into the flight: gravity alone
    /// brings them down, walls and rocks still stop them, and a slow-fall cape still slows the fall.
    /// The flight ends on touching the ground, in water, when something holds them, at death, or after
    /// <see cref="Longest"/> seconds in any case. No fall damage lands from the moment of the throw until
    /// <see cref="Settle"/> seconds after it ends (<see cref="Forgives"/>, checked where the game sends its fall hit), so
    /// a bounce or a slide off the landing spot is soft too; a fall that starts after that counts in full, measured as
    /// the game always does from the last ground they stood on. Nothing else is forgiven: the game deals no damage for
    /// striking a wall or a rock, and a moving log or cart hits a thrown player as it would a standing one.
    /// </summary>
    internal static class BrutalFlight
    {
        /// <summary>Seconds after the throw before touching ground counts as landing: the game's own jump guard.</summary>
        private const float Takeoff = 0.1f;

        /// <summary>Seconds a flight lasts at most, landed or not - caught on a ledge or in a tree, it ends anyway.</summary>
        private const float Longest = 10f;

        /// <summary>Seconds after landing during which a fall still lands softly.</summary>
        private const float Settle = 1f;

        private static Player? _flier;
        private static float _thrownAt;
        private static float _softUntil;

        /// <summary>True while this machine's player is in the air from a throw; the flight patch reads only this.</summary>
        public static bool Active { get; private set; }

        /// <summary>True while <paramref name="player"/> is in the air from a throw.</summary>
        public static bool Flying(Player player) => Active && player == _flier && Time.time - _thrownAt <= Longest;

        /// <summary>Throws the local player: from now they fly, and no fall hurts them until they have landed.</summary>
        public static void Launch(Player player, Vector3 velocity)
        {
            player.ForceJump(velocity, effects: false);
            _flier = player;
            _thrownAt = Time.time;
            _softUntil = _thrownAt + Longest + Settle;
            Active = true;
        }

        /// <summary>
        /// One physics step of the thrown player's own movement: while they are in the air, the velocity the game is
        /// about to apply is the body's own, so the throw alone carries them; on landing, the flight ends.
        /// </summary>
        public static void Steer(Player player, ref Vector3 velocity)
        {
            if (player != _flier)
            {
                return;
            }
            if (Landed(player))
            {
                Active = false;
                _softUntil = Time.time + Settle;
                return;
            }
            velocity = player.m_body.linearVelocity; // nothing to add: the walking code applies no change
        }

        /// <summary>True when the game's fall hit for <paramref name="character"/> comes from a throw and lands softly.</summary>
        public static bool Forgives(Character character, HitData hit) =>
            hit.m_hitType == HitData.HitType.Fall && character == _flier && Time.time < _softUntil;

        // Back on the ground (the game's own test, past the take-off), in water, held by a seat or a saddle, dead, or
        // too long in the air.
        private static bool Landed(Player player)
        {
            float aloft = Time.time - _thrownAt;
            return (aloft > Takeoff && player.IsOnGround()) || player.IsSwimming() || player.IsAttached()
                || player.IsDead() || aloft > Longest;
        }
    }
}

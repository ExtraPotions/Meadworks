// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One formed Nightfall tornado's hunt, as the boss's owner moves it, across the ground plane (each machine lays it
    /// on its own ground). It turns toward its player where the owner sees them, at most <see cref="TurnRate"/> a
    /// second so it sweeps round rather than snapping, and moves at the wave's speed; on reaching them it sits on them
    /// until they move. When its player is gone - dead, teleporting, a ghost, logged off or out of this machine's
    /// world - it carries on straight the way it was going, still a hazard to anyone in its path, and takes up the hunt
    /// again should they come back.
    /// </summary>
    internal sealed class TornadoPath
    {
        /// <summary>Radians a second the tornado turns toward its player.</summary>
        private const float TurnRate = Mathf.PI;

        public readonly ZDOID Target;
        public Vector3 Position;

        /// <summary>The way it moves: flat and of unit length.</summary>
        public Vector3 Heading;

        private bool _moving;

        public TornadoPath(ZDOID target, Vector3 position, Vector3 heading)
        {
            Target = target;
            Position = new Vector3(position.x, 0f, position.z);
            Heading = StormTargets.Flat(heading);
            if (Heading == Vector3.zero)
            {
                Heading = Vector3.forward;
            }
        }

        /// <summary>Its velocity at <paramref name="speed"/>; zero while it sits on its player.</summary>
        public Vector3 Velocity(float speed) => _moving ? Heading * speed : Vector3.zero;

        public void Step(float dt, float speed)
        {
            Player? quarry = Quarry();
            float step = speed * dt;
            if (quarry == null)
            {
                Position += Heading * step; // lost: straight on
                _moving = step > 0f;
                return;
            }
            Vector3 to = quarry.transform.position - Position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f)
            {
                Heading = Vector3.RotateTowards(Heading, to.normalized, TurnRate * dt, 0f);
            }
            _moving = to.magnitude > step;
            Position = _moving ? Position + Heading * step : new Vector3(Position.x + to.x, 0f, Position.z + to.z);
        }

        /// <summary>Its player while they can be hunted on this machine; null otherwise.</summary>
        private Player? Quarry()
        {
            GameObject? body = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(Target) : null;
            Player? player = body != null ? body.GetComponent<Player>() : null;
            return player != null && !player.IsDead() && !player.IsTeleporting() && !player.InGhostMode()
                ? player
                : null;
        }
    }
}

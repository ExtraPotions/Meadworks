// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Text;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Bifrost Step (effect <c>apportation</c>), on the thrower's own client, the owner of the thrower's player: a thrown
    /// weapon that hits an enemy (a hostile creature, or a player in PvP) moves the thrower to the impact, beside it on the side
    /// the throw came from, facing it. The move is local and instant (not the game's portal teleport, which fades for two
    /// seconds); the player's position replicates through the game's own sync, so every client sees the step.
    /// <para>
    /// Safe spot (judgement calls): the target's radius plus the thrower's plus a hand's gap from the target, with a clear
    /// line from the target's centre (never through a wall), standing on a floor found straight below within the target's
    /// own height plus 3 m (never a fall, never into the void beneath a flying target), and room for the thrower's
    /// capsule there. No such spot: the thrower stays and is told. A thrower who is sitting, steering, riding,
    /// teleporting or dead never moves. The fall height is reset at the new spot, so the step never deals fall damage.
    /// </para>
    /// </summary>
    internal static class Apportation
    {
        private const float Gap = 0.4f;
        private const float MaxDrop = 3f;
        private const float Lift = 0.3f;
        private const float ProbeHeight = 1f;

        private static int _floorMask, _solidMask;

        public static void TryBlink(Player thrower, GameObject hit)
        {
            Character target = hit.GetComponent<Character>();
            if (target == null || ReferenceEquals(target, thrower) || !IsFoe(thrower, target) || !CanMove(thrower))
            {
                return;
            }
            Masks();
            if (FindSpot(thrower, target, out Vector3 spot))
            {
                Blink(thrower, spot, target.transform.position);
            }
            else
            {
                thrower.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_msg_apport_blocked"));
            }
        }

        // An enemy, a neutral this very hit turns hostile (the Dvergr), or a player hit in PvP; never a tame or a friend
        // (a PvP thrower's projectile may hit those too).
        private static bool IsFoe(Player thrower, Character target)
        {
            if (target.IsPlayer())
            {
                return thrower.IsPVPEnabled();
            }
            BaseAI ai = target.GetBaseAI();
            return BaseAI.IsEnemy(thrower, target) || (ai != null && ai.IsAggravatable() && !target.IsTamed());
        }

        private static bool CanMove(Player player) =>
            !player.IsDead() && !player.IsAttached() && !player.IsTeleporting() && !player.InIntro() && !player.IsDebugFlying();

        private static bool FindSpot(Player thrower, Character target, out Vector3 spot)
        {
            Vector3 at = target.transform.position;
            Vector3 away = thrower.transform.position - at;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -target.transform.forward;
            Vector3 centre = target.GetCenterPoint();
            Vector3 probe = at + away * (target.GetRadius() + thrower.GetRadius() + Gap);
            probe.y = Mathf.Max(centre.y, at.y + ProbeHeight);
            spot = probe;
            if (Physics.Linecast(centre, probe, _solidMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            float reach = probe.y - at.y + MaxDrop;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit floor, reach, _floorMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            spot = floor.point;
            return HasRoom(thrower, spot);
        }

        private static bool HasRoom(Player player, Vector3 spot)
        {
            float radius = player.GetRadius();
            Vector3 bottom = spot + Vector3.up * (radius + Lift);
            Vector3 top = spot + Vector3.up * Mathf.Max(radius + Lift, player.GetHeight() - radius);
            return !Physics.CheckCapsule(bottom, top, radius * 0.9f, _solidMask, QueryTriggerInteraction.Ignore);
        }

        // As the game's own teleport lands a player: position, no velocity, the fall height measured from here.
        private static void Blink(Player player, Vector3 spot, Vector3 lookAt)
        {
            Vector3 position = spot + Vector3.up * 0.05f;
            player.transform.position = position;
            player.m_body.position = position;
            player.m_body.linearVelocity = Vector3.zero;
            player.m_maxAirAltitude = position.y;
            Vector3 look = lookAt - position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                player.SetLookDir(look.normalized);
            }
        }

        private static void Masks()
        {
            if (_floorMask != 0)
            {
                return;
            }
            _floorMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
            _solidMask = LayerMask.GetMask("Default", "static_solid", "piece", "terrain", "vehicle", "blocker");
        }
    }
}

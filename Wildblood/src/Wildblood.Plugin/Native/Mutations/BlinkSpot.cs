// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Where a Blinking creature lands: `distance` metres behind its target, opposite the way the target faces, then
    /// behind-left and behind-right when that spot will not do. A spot has to be real ground - the floor found by a ray
    /// from just above the target's feet, so a roof overhead never counts, and within a step of the target's own
    /// height, so a drop or a ledge behind it never counts either. It has to be free of walls, pieces and bodies (the
    /// target's included, so it never lands on its quarry), in plain sight of the target (never the far side of a wall
    /// or a hill), somewhere the creature's own AI would go (no water it avoids, no lava, no fire it fears, no trader's
    /// peace), and joined to where it stands by the navmesh, so it never reaches a roof or a walled base it could not
    /// reach on foot. A flying creature keeps its height above the ground. Pure queries, on the owner.
    /// </summary>
    internal static class BlinkSpot
    {
        /// <summary>Straight behind first, then behind-left and behind-right.</summary>
        private static readonly float[] Angles = { 0f, -40f, 40f };

        /// <summary>The floor ray starts this far above the target's feet, under any ceiling above it.</summary>
        private const int RayStart = 2;

        /// <summary>Floor further than this from the target's feet is a ledge or a drop, not "behind it".</summary>
        private const float MaxStep = 2f;

        /// <summary>Set down this far above the floor, as a spawn is, so it never starts inside the ground.</summary>
        private const float GroundLift = 0.1f;

        /// <summary>A flyer's kept height is capped, so one circling high never lands out of reach.</summary>
        private const float MaxHover = 12f;

        /// <summary>The body test runs a little slim, so ground clutter does not rule out every spot.</summary>
        private const float Slim = 0.85f;

        private static readonly int BodyMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece",
            "vehicle", "blocker", "character", "character_net", "character_ghost", "character_noenv");

        private static readonly int SightMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece",
            "terrain", "vehicle");

        private static readonly Collider[] Hits = new Collider[16];

        /// <summary>The first good spot behind the target, or false when none of the three will do.</summary>
        public static bool Find(Character me, BaseAI ai, Character target, float distance, out Vector3 dest)
        {
            Vector3 back = Behind(me, target);
            float reach = Mathf.Max(distance, Radius(target) + Radius(me) + 0.5f); // never onto the target itself
            foreach (float angle in Angles)
            {
                Vector3 probe = target.transform.position + Quaternion.Euler(0f, angle, 0f) * back * reach;
                if (TryPlace(me, ai, target, probe, out dest))
                {
                    return true;
                }
            }
            dest = Vector3.zero;
            return false;
        }

        /// <summary>Opposite the way the target faces; failing that, the far side from the creature.</summary>
        private static Vector3 Behind(Character me, Character target)
        {
            Vector3 facing = Flat(target.transform.forward);
            return facing != Vector3.zero ? -facing : Flat(target.transform.position - me.transform.position);
        }

        private static bool TryPlace(Character me, BaseAI ai, Character target, Vector3 probe, out Vector3 dest)
        {
            dest = probe;
            if (!Floor(probe, target.transform.position.y, out float floor))
            {
                return false;
            }
            dest.y = floor + Lift(me);
            return IsClear(me, dest) && InSight(me, target, dest) && Walkable(me, ai, dest);
        }

        /// <summary>
        /// Solid, still ground under the probe near the target's height. The game's own floor query skips anything on a
        /// rigidbody, so the deck of a moving ship is never a destination.
        /// </summary>
        private static bool Floor(Vector3 probe, float feet, out float floor)
        {
            floor = 0f;
            probe.y = feet;
            ZoneSystem zones = ZoneSystem.instance;
            return zones != null && zones.GetSolidHeight(probe, out floor, RayStart)
                && Mathf.Abs(floor - feet) <= MaxStep;
        }

        /// <summary>How high above the floor it lands: just above it, or a flyer's height over land or sea.</summary>
        private static float Lift(Character me)
        {
            ZoneSystem zones = ZoneSystem.instance;
            if (!me.IsFlying() || zones == null || !zones.GetSolidHeight(me.transform.position, out float ground, 1))
            {
                return GroundLift;
            }
            float surface = Mathf.Max(ground, zones.m_waterLevel);
            return Mathf.Clamp(me.transform.position.y - surface, GroundLift, MaxHover);
        }

        /// <summary>No wall, piece or body - its own excepted - where it would stand. Checked on arrival too.</summary>
        internal static bool IsClear(Character me, Vector3 dest)
        {
            float radius = Radius(me) * Slim;
            float bottom = radius + 0.2f;
            float top = Mathf.Max(bottom, Height(me) - radius);
            int count = Physics.OverlapCapsuleNonAlloc(dest + Vector3.up * bottom, dest + Vector3.up * top, radius,
                Hits, BodyMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Hits[i] != null && !Hits[i].transform.IsChildOf(me.transform))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>In plain view of the target: the same room, never the far side of a wall or a hill.</summary>
        private static bool InSight(Character me, Character target, Vector3 dest)
        {
            Vector3 chest = dest + Vector3.up * (Height(me) * 0.5f);
            return !Physics.Linecast(target.GetCenterPoint(), chest, SightMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// The creature's own AI would go there (its water, lava and fire rules), a wild creature never lands inside a
        /// no-monster area, and the navmesh joins it to where it stands (a flyer always passes, by the game's rule).
        /// </summary>
        private static bool Walkable(Character me, BaseAI ai, Vector3 dest)
        {
            return ai.IsValidRandomMovePoint(dest)
                && (me.IsTamed() || EffectArea.IsPointInsideNoMonsterArea(dest) == null)
                && ai.HavePath(dest);
        }

        /// <summary>The body's radius at its scale, from its capsule (GetRadius overstates a scaled body).</summary>
        internal static float Radius(Character character)
        {
            CapsuleCollider body = character.GetComponent<CapsuleCollider>();
            if (body == null)
            {
                return 0.5f;
            }
            Vector3 scale = body.transform.lossyScale;
            return Mathf.Max(0.2f, body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        }

        /// <summary>From the creature's feet (its origin) to the top of its body, at its scale.</summary>
        internal static float Height(Character character)
        {
            CapsuleCollider body = character.GetComponent<CapsuleCollider>();
            return body != null ? Mathf.Max(0.5f, body.bounds.max.y - character.transform.position.y) : 2f;
        }

        /// <summary>A direction flattened to the ground and normalised; zero when it has no horizontal part.</summary>
        internal static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }
}

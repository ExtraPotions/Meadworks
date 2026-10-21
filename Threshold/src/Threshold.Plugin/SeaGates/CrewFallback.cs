// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/CrewFallback.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>Where a crew member goes ashore when the ship never settled in time: dry ground beside a destination
    /// pillar, else a pillar's top (pillars stand on land or in water at most 2 m deep, so their tops are always dry),
    /// never before the area around it is loaded, never in open water, never in the air. Heights by
    /// <c>ZoneSystem.GetSolidHeight(p, out h)</c> (verified in the decompiled assembly): a ray from 1000 m above down the
    /// solid layers (Default, static_solid, Default_small, piece, terrain), refused when the first hit belongs to a
    /// rigidbody (a ship, a dropped item). A spot counts as dry when that height stands at least 0.3 m above sea level
    /// and within 3 m of the terrain under it (so not a tree trunk's top or a high roof). Spots are tried 2 to 8 m out
    /// from each pillar (a gate built in the shallows has its shore a little further off), starting on the side away
    /// from the gate (usually inland); then the top of a pillar; and when the area has been loaded for 10 s with
    /// neither, the highest spot sampled. With no pillar known at all, the player goes back where they jumped from,
    /// once that area is loaded.</summary>
    internal static class CrewFallback
    {
        private const float SearchSeconds = 0.25f;
        private const float GiveUpSeconds = 10f;
        private const float DryMargin = 0.3f;
        private const float MaxAboveGround = 3f;
        private const float Lift = 0.05f;
        private const float HoldOut = 2f;
        private const float HoldUp = 1f;
        private const int Directions = 8;
        private static readonly float[] Radii = { 2f, 3f, 4f, 6f, 8f };

        private static float nextSearch;
        private static float readySince;
        private static bool haveBest;
        private static Vector3 best;

        internal static void Reset()
        {
            nextSearch = 0f;
            readySince = -1f;
            haveBest = false;
        }

        /// <summary>Where the player waits for the fallback: 2 m out from the anchor pillar (else the partner) and 1 m
        /// up, beside it rather than inside it, so the game streams the area the search needs.</summary>
        internal static void HoldPoint(JumpOrder order, Player p, out Vector3 pos, out Quaternion rot)
        {
            Vector3 pillar = Known(order.DestAnchor) ? order.DestAnchor : order.DestPartner;
            if (!Known(pillar))
            {
                pos = p.m_teleportFromPos;
                rot = p.m_teleportFromRot;
                return;
            }
            Vector3 mid = Mid(order);
            pos = pillar + Outward(pillar, mid) * HoldOut + Vector3.up * HoldUp;
            rot = CrewSpot.Facing(mid - pos);
        }

        /// <summary>A safe spot once the area is ready, a few times a second.</summary>
        internal static bool TryFind(JumpOrder order, Player p, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            if (Time.time < nextSearch || ZNetScene.instance == null || ZoneSystem.instance == null)
                return false;
            nextSearch = Time.time + SearchSeconds;
            if (!Known(order.DestAnchor) && !Known(order.DestPartner))
                return BackToStart(p, out pos, out rot);
            if (!Search(order, out pos) && !GivenUp(out pos))
                return false;
            rot = CrewSpot.Facing(Mid(order) - pos);
            return true;
        }

        private static bool Search(JumpOrder order, out Vector3 pos)
        {
            Vector3 mid = Mid(order);
            bool anchorReady = Ready(order.DestAnchor);
            bool partnerReady = Ready(order.DestPartner);
            if ((anchorReady || partnerReady) && readySince < 0f)
                readySince = Time.time;
            if (anchorReady && Around(order.DestAnchor, mid, out pos))
                return true;
            if (partnerReady && Around(order.DestPartner, mid, out pos))
                return true;
            if (anchorReady && OnTop(order.DestAnchor, out pos))
                return true;
            if (partnerReady && OnTop(order.DestPartner, out pos))
                return true;
            pos = Vector3.zero;
            return false;
        }

        private static bool Around(Vector3 pillar, Vector3 mid, out Vector3 pos)
        {
            Vector3 outward = Outward(pillar, mid);
            foreach (float radius in Radii)
            {
                for (int i = 0; i < Directions; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, Turn(i), 0f) * outward;
                    if (DryAt(pillar + direction * radius, out pos))
                        return true;
                }
            }
            pos = Vector3.zero;
            return false;
        }

        /// <summary>0, +45, -45, +90, -90, +135, -135, 180 degrees: the outward side first, the gate's side last.</summary>
        private static float Turn(int i)
        {
            int step = (i + 1) / 2;
            float sign = i % 2 == 1 ? 1f : -1f;
            return step * 45f * sign;
        }

        private static bool DryAt(Vector3 point, out Vector3 pos)
        {
            pos = point;
            ZoneSystem zones = ZoneSystem.instance;
            if (!zones.IsZoneLoaded(point) || !zones.GetSolidHeight(point, out float solid))
                return false;
            pos.y = solid + Lift;
            Remember(pos);
            if (solid < SeaGateFields.WaterLevel + DryMargin)
                return false;
            return !zones.GetGroundHeight(point, out float ground) || solid - ground <= MaxAboveGround;
        }

        private static bool OnTop(Vector3 pillar, out Vector3 pos)
        {
            pos = pillar;
            if (!ZoneSystem.instance.GetSolidHeight(pillar, out float top) || top < SeaGateFields.WaterLevel + DryMargin)
                return false;
            pos.y = top + Lift;
            return true;
        }

        private static void Remember(Vector3 pos)
        {
            if (haveBest && pos.y <= best.y)
                return;
            best = pos;
            haveBest = true;
        }

        private static bool GivenUp(out Vector3 pos)
        {
            pos = best;
            if (!haveBest || readySince < 0f || Time.time - readySince < GiveUpSeconds)
                return false;
            SeaGateLog.LogWarning($"Sea gate: no dry ground beside the destination gate; setting the player on the highest spot found ({pos}).");
            return true;
        }

        private static bool BackToStart(Player p, out Vector3 pos, out Quaternion rot)
        {
            pos = p.m_teleportFromPos;
            rot = p.m_teleportFromRot;
            if (!ZNetScene.instance.IsAreaReady(pos))
                return false;
            SeaGateLog.LogError("Sea gate: the jump order named no destination pillars; returning the player to where they jumped from.");
            return true;
        }

        private static bool Ready(Vector3 pillar) => Known(pillar) && ZNetScene.instance.IsAreaReady(pillar);

        private static bool Known(Vector3 pillar) => pillar != Vector3.zero;

        private static Vector3 Mid(JumpOrder order)
        {
            if (Known(order.DestAnchor) && Known(order.DestPartner))
                return (order.DestAnchor + order.DestPartner) * 0.5f;
            return Known(order.DestAnchor) ? order.DestAnchor : order.DestPartner;
        }

        /// <summary>Level, from the gate's middle out through the pillar; world forward when the two coincide.</summary>
        private static Vector3 Outward(Vector3 pillar, Vector3 mid)
        {
            Vector3 outward = pillar - mid;
            outward.y = 0f;
            return outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;
        }
    }
}

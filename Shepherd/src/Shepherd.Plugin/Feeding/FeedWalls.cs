// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Shepherd.Feeding;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    public static class FeedWalls
    {
        private static readonly int SolidMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

        public static bool InReach(MonsterAI animal, Container source, bool requireWall = false)
            => From(animal, source, animal.transform.position, requireWall);

        public static bool From(MonsterAI animal, Container source, Vector3 footing, bool requireWall = false)
        {
            Vector3 from = footing + Vector3.up * .5f;
            Vector3 to = source.transform.position + Vector3.up * .5f;
            float distance = Vector3.Distance(from, to);
            if (distance < .001f) return !requireWall;
            Vector3 direction = (to - from) / distance;
            var spans = new List<FeedSolidSpan>();
            Transform sourceRoot = source.m_nview != null ? source.m_nview.transform : source.transform;
            foreach (Collider collider in Physics.OverlapCapsule(from, to, .001f, SolidMask, QueryTriggerInteraction.Ignore))
            {
                if (collider.transform.IsChildOf(sourceRoot) || collider.transform.IsChildOf(animal.transform)
                    || collider.GetComponentInParent<Character>() != null) continue;
                // Cast from outside each collider so an animal already inside a solid cannot bypass the entry hit.
                float margin = collider.bounds.size.magnitude + distance + 1f;
                float length = distance + 2f * margin;
                bool entered = collider.Raycast(new Ray(from - direction * margin, direction), out var entry, length);
                bool exited = collider.Raycast(new Ray(to + direction * margin, -direction), out var exit, length);
                if (!entered && !exited) continue;
                if (!entered || !exited) return false;
                float start = Mathf.Max(0, entry.distance - margin);
                float end = Mathf.Min(distance, distance + margin - exit.distance);
                if (end <= start) continue;
                if (collider.gameObject.layer == LayerMask.NameToLayer("terrain")
                    || Mathf.Abs(entry.normal.y) > .5f || Mathf.Abs(exit.normal.y) > .5f) return false;
                spans.Add(new FeedSolidSpan(start, end));
            }
            return (!requireWall || spans.Count > 0)
                && FeedMath.WallWithinReach(FeedMath.SolidThickness(spans), ShepherdSettings.ReachThroughWalls.Value);
        }
    }
}

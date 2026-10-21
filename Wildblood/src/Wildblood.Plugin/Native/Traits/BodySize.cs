// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Traits
{
    /// <summary>
    /// Which creatures are large - trolls, bears, lox, golems, Fuling berserkers - decided here and nowhere else. The game
    /// has no size category, so the body is measured: the CapsuleCollider the game moves it by, on its prefab and at the
    /// prefab's own scale (never ECR's star growth), is large when its radius is a metre or more or it is 3.5 m or more
    /// long, on a creature with at least 300 base health, so a big body with little in it (an Oozer, a lox calf) is not
    /// a heavyweight. A modded creature is measured the same way, so one built on a troll's body is large with no list to
    /// keep. Bosses are their own rule, not this one. The verdict is kept per prefab, and every machine measures the same
    /// prefab, so every machine agrees with nothing synced. Two rules read it: a large kind never rolls or inherits
    /// Gilded or Relentless (<see cref="Barred"/>), and a large creature is never a Devouring creature's prey.
    /// </summary>
    public static class BodySize
    {
        /// <summary>What a large kind never takes, packed like a trait mask: Gilded and Relentless.</summary>
        public const int LargeBars = (1 << (int)Mutation.Gilded) | (1 << (int)Mutation.Relentless);

        private const float LargeRadius = 1f;
        private const float LargeLength = 3.5f;
        private const float MinHealth = 300f;

        // A centimetre of slack, so a capsule built at exactly a metre (the Troll's, the Stone Golem's) or 3.5 m (the
        // Fuling berserker's) counts whatever the float rounding of its scale.
        private const float Slack = 0.01f;

        private static readonly Dictionary<int, bool> Verdicts = new Dictionary<int, bool>();

        /// <summary>True when the creature's body, as its prefab builds it, is large.</summary>
        public static bool IsLarge(Character creature)
        {
            int hash = PrefabHash(creature);
            if (Verdicts.TryGetValue(hash, out bool large))
            {
                return large;
            }
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(hash) : null;
            // No registered prefab (none in practice: a networked creature cannot spawn without one): the live collider
            // in its own units, its scale left out since ECR's star growth is part of it, and not kept.
            return prefab != null ? Verdict(hash, prefab) : Measure(creature.gameObject, Vector3.one);
        }

        /// <summary>
        /// The mutations this creature never rolls or inherits, packed like a trait mask: <see cref="LargeBars"/> when it
        /// is large or grows up into a large creature (a lox calf becomes a lox and keeps its traits), else none.
        /// </summary>
        public static int Barred(Character creature) =>
            IsLarge(creature) || GrowsLarge(creature.GetComponent<Growup>()) ? LargeBars : 0;

        // A young creature's kind is the adult it becomes: large when any prefab it can grow into is.
        private static bool GrowsLarge(Growup? young)
        {
            if (young == null)
            {
                return false;
            }
            bool large = IsLargePrefab(young.m_grownPrefab);
            foreach (Growup.GrownEntry entry in young.m_altGrownPrefabs ?? new List<Growup.GrownEntry>())
            {
                large |= IsLargePrefab(entry.m_prefab);
            }
            return large;
        }

        private static bool IsLargePrefab(GameObject? prefab) =>
            prefab != null && Verdict(prefab.name.GetStableHashCode(), prefab);

        private static bool Verdict(int hash, GameObject prefab)
        {
            if (!Verdicts.TryGetValue(hash, out bool large))
            {
                large = Measure(prefab, prefab.transform.localScale);
                Verdicts[hash] = large;
            }
            return large;
        }

        // Unity scales a capsule's length by the scale along its axis and its radius by the larger of the other two, so
        // a stretched prefab is measured as the game moves it. A four-legged body's capsule lies along its back.
        private static bool Measure(GameObject body, Vector3 scale)
        {
            CapsuleCollider capsule = body.GetComponent<CapsuleCollider>();
            Character character = body.GetComponent<Character>();
            if (capsule == null || character == null || character.m_health < MinHealth)
            {
                return false;
            }
            int axis = Mathf.Clamp(capsule.direction, 0, 2);
            float along = Mathf.Abs(scale[axis]);
            float across = Mathf.Max(Mathf.Abs(scale[(axis + 1) % 3]), Mathf.Abs(scale[(axis + 2) % 3]));
            return capsule.radius * across >= LargeRadius - Slack || capsule.height * along >= LargeLength - Slack;
        }

        // Off the ZDO, with no string work: the enmity check asks this for every creature a feeding devourer considers.
        private static int PrefabHash(Character creature)
        {
            ZNetView nview = creature.m_nview;
            ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            return zdo != null ? zdo.GetPrefab() : Utils.GetPrefabName(creature.gameObject).GetStableHashCode();
        }
    }
}

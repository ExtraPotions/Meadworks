// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Crossbow
{
    /// <summary>
    /// The game's skeleton that can come as an archer, and the crossbowman that may come in its place: a copy of that
    /// skeleton (its health, resistances, looks, sounds and drops) whose crossbow hits as hard as its bow, as blunt.
    /// Only the Black Forest's Skeleton (40 health, a 20-damage bow): the user wants one version of each skeleton, so
    /// the Meadows', Swamps' and Mountains' archers are left alone, as are the no-archer, poison, Hildir and summoned
    /// skeletons.
    /// </summary>
    public sealed class XbowKind
    {
        public static readonly XbowKind[] All =
        {
            new XbowKind("Skeleton", "skeleton_bow", "ECP_SkeletonCrossbowman"),
        };

        /// <summary>The game's skeleton prefab.</summary>
        public readonly string Skeleton;

        /// <summary>Its archer's bow, one of its random weapons (not a network prefab).</summary>
        public readonly string Bow;

        /// <summary>The crossbowman's prefab, and its shot's.</summary>
        public readonly string Creature, Shot;

        /// <summary>The crossbowman, once built; null until the first ZNetScene wakes, or when the game lacks the skeleton.</summary>
        public GameObject? Prefab;

        /// <summary>The damage of the skeleton's own bow, all kinds added (the crossbow deals it as blunt, times `Damage Factor`).</summary>
        public float BowDamage;

        private XbowKind(string skeleton, string bow, string creature)
        {
            (Skeleton, Bow, Creature, Shot) = (skeleton, bow, creature, creature + "_shot");
        }

        /// <summary>The kind whose skeleton this prefab is, or null.</summary>
        public static XbowKind? OfSkeleton(GameObject? prefab) => prefab == null ? null : All.FirstOrDefault(k => k.Skeleton == prefab.name);

        /// <summary>The kind whose crossbowman this creature is (by its prefab name), or null.</summary>
        public static XbowKind? OfCreature(string prefabName) => All.FirstOrDefault(k => k.Creature == prefabName);
    }
}

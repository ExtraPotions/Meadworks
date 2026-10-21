// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The loaded creatures whose body can be hidden on this machine - Cloaked beyond its reveal distance, Blinking
    /// mid-jump, Cloning behind its decoy - by creature, so the nameplate test the game makes for every creature near the
    /// player each frame (<see cref="Patches.EnemyHudCloakPatch"/>) finds them with one lookup instead of a component
    /// search apiece, and costs nothing while none is loaded. Each behaviour joins as it sets up and leaves as it is
    /// destroyed with its creature; what hides the plate is still each one's own live state.
    /// </summary>
    internal static class PlateVeils
    {
        private sealed class Parts
        {
            public CloakBehaviour? Cloak;
            public BlinkBehaviour? Blink;
            public CloneBehaviour? Clone;

            public bool Empty => Cloak is null && Blink is null && Clone is null;
        }

        private static readonly Dictionary<Character, Parts> ByCreature = new Dictionary<Character, Parts>();

        /// <summary>True while this creature's body is hidden here, so its nameplate must hide with it.</summary>
        public static bool Hides(Character creature)
        {
            if (ByCreature.Count == 0 || !ByCreature.TryGetValue(creature, out Parts parts))
            {
                return false;
            }
            return (parts.Cloak != null && parts.Cloak.Hidden) || (parts.Blink != null && parts.Blink.Veiled)
                || (parts.Clone != null && parts.Clone.Hidden);
        }

        public static void Join(Character creature, CloakBehaviour cloak) => Of(creature).Cloak = cloak;

        public static void Join(Character creature, BlinkBehaviour blink) => Of(creature).Blink = blink;

        public static void Join(Character creature, CloneBehaviour clone) => Of(creature).Clone = clone;

        // `is null` rather than Unity's ==: the creature may already read as destroyed here, and its entry must go.
        public static void Leave(Character? creature, MonoBehaviour part)
        {
            if (creature is null || !ByCreature.TryGetValue(creature, out Parts parts))
            {
                return;
            }
            if (ReferenceEquals(parts.Cloak, part))
            {
                parts.Cloak = null;
            }
            if (ReferenceEquals(parts.Blink, part))
            {
                parts.Blink = null;
            }
            if (ReferenceEquals(parts.Clone, part))
            {
                parts.Clone = null;
            }
            if (parts.Empty)
            {
                ByCreature.Remove(creature);
            }
        }

        private static Parts Of(Character creature)
        {
            if (!ByCreature.TryGetValue(creature, out Parts parts))
            {
                parts = new Parts();
                ByCreature[creature] = parts;
            }
            return parts;
        }
    }
}

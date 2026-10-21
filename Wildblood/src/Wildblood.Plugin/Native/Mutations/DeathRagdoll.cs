// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Names the ragdoll a death has just made, so a Bloated corpse is always its own creature's and never a neighbour
    /// lying nearby. The game makes a dying creature's ragdoll inside <c>Character.OnDeath</c>, on the creature's owner,
    /// and sets it up there (<c>Ragdoll.Setup</c>, see <see cref="Patches.DeathRagdollPatch"/>) before the death patch's
    /// postfix runs - all in one call. So the ragdoll set up last, in this same frame, lying where the creature died, is
    /// the one this death made; a creature that leaves no ragdoll finds none, even when a neighbour's was set up earlier
    /// in the same frame by the same blow.
    /// </summary>
    public static class DeathRagdoll
    {
        /// <summary>How far from the creature's feet its ragdoll may be spawned and still be its own, in metres.</summary>
        private const float SameSpot = 1f;

        private static Ragdoll? _last;
        private static int _lastFrame = -1;

        /// <summary>Owner side, as the game sets up a newly made ragdoll.</summary>
        public static void Made(Ragdoll ragdoll)
        {
            _last = ragdoll;
            _lastFrame = Time.frameCount;
        }

        /// <summary>Owner side, later in the same death: the ragdoll it made at <paramref name="deathPos"/>, or None.</summary>
        public static ZDOID MadeAt(Vector3 deathPos)
        {
            bool ours = _last != null && _lastFrame == Time.frameCount
                && (_last.transform.position - deathPos).sqrMagnitude <= SameSpot * SameSpot;
            return ours ? CorpseBurst.IdOf(_last) : ZDOID.None;
        }
    }
}

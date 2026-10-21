// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What one trail kind's patches do to this machine's own player - the only machine that can feel them, and the one
    /// that decides it, from the patches it has drawn itself. Standing in a patch on the ground, they wear the kind's
    /// status (<see cref="TrailStatus"/>), slowed by the strongest patch under them and, on ice, gripping only as much
    /// as the slipperiest one allows (<see cref="IceFooting"/>). Off the patches, in the air, swimming, riding, sitting
    /// or in ghost mode, they feel nothing new, and the status runs out after the kind's linger. Patches never touch a
    /// creature: only a player's own machine looks at them, and only for that player.
    /// </summary>
    internal sealed class PatchFooting
    {
        private readonly TrailKind _kind;
        private float _grip = 1f;
        private float _nextBurn;

        public PatchFooting(TrailKind kind) => _kind = kind;

        /// <summary>One look at where <paramref name="player"/> stands among <paramref name="patches"/>.</summary>
        public void Tick(Player? player, List<GroundPatch> patches)
        {
            if (player == null || player.IsDead())
            {
                Drop();
                return;
            }
            if (Feels(player) && Under(player.transform.position, patches, out float slow, out float grip, out float burn))
            {
                if (TrailStatus.Hold(player, _kind, slow) != null)
                {
                    _grip = grip;
                }
                if (burn > 0f && Time.time >= _nextBurn)
                {
                    _nextBurn = Time.time + 1f;
                    FireGround.Ignite(player, burn);
                }
            }
            else
            {
                TrailStatus.Release(player, _kind);
            }
            if (_kind.Slips)
            {
                // the slide lasts exactly as long as the status
                IceFooting.Set(TrailStatus.On(player, _kind) != null ? _grip : 1f);
            }
        }

        /// <summary>No player to hold, or this machine's patches are gone: full grip again.</summary>
        public void Drop()
        {
            _grip = 1f;
            if (_kind.Slips)
            {
                IceFooting.Clear();
            }
        }

        private static bool Feels(Player player) =>
            player.IsOnGround() && !player.IsSwimming() && !player.IsAttached() && !player.InGhostMode()
            && !player.IsDebugFlying();

        // The strongest slow and the least grip among the patches the feet stand in; false when they stand in none.
        private static bool Under(Vector3 feet, List<GroundPatch> patches, out float slow, out float grip, out float burn)
        {
            slow = 0f;
            grip = 1f;
            burn = 0f;
            bool any = false;
            for (int i = 0; i < patches.Count; i++)
            {
                GroundPatch patch = patches[i];
                if (patch.Covers(feet))
                {
                    any = true;
                    slow = Mathf.Max(slow, patch.Slow);
                    grip = Mathf.Min(grip, patch.Grip);
                    burn = Mathf.Max(burn, patch.Burn);
                }
            }
            return any;
        }
    }
}

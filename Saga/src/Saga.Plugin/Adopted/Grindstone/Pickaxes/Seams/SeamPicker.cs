// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/SeamPicker.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Chooses a seam's chunk on the miner's own client, at random among the rock's chunks that
    /// <list type="bullet">
    /// <item>still stand here (<see cref="IsStanding"/>) and outlast the swing that opens the seam
    /// (<see cref="Outlasts"/>): the chunk being mined is a candidate unless the swing broke it;</item>
    /// <item>the miner can strike from where they stand: the chunk's bounds within the equipped pickaxe's reach
    /// (Attack.m_attackRange, from the swing's origin at m_attackHeight) plus <see cref="ReachSlack"/>;</item>
    /// <item>the miner can see: a line from the player's eye to the chunk's centre meets that chunk before anything
    /// else (another chunk, the ground). A buried or inner chunk would be a seam nobody can strike. A chunk the swing
    /// touched needs no such line: the pickaxe just reached it.</item>
    /// </list>
    /// </summary>
    internal static class SeamPicker
    {
        /// <summary>Metres a seam's chunk may lie beyond the pickaxe's reach: leaning into the next swing.</summary>
        private const float ReachSlack = 0.3f;

        /// <summary>Reach and swing height when the equipped weapon has no attack (never for a pickaxe).</summary>
        private const float FallbackReach = 2f;
        private const float FallbackHeight = 1f;

        private static readonly List<int> Candidates = new List<int>();
        private static int sightMask;

        /// <summary>A seam's chunk for the rock of <paramref name="swing"/>, the swing that opens it; -1 when none fits.</summary>
        public static int Pick(SeamNote swing)
        {
            Player player = Player.m_localPlayer;
            Rock rock = swing.Rock;
            if (player == null || rock == null || !rock.IsValid)
                return -1;
            float reach = SwingReach(player, out Vector3 origin);
            Vector3 eye = player.GetEyePoint();
            Candidates.Clear();
            int count = RockChunks.Count(rock);
            for (int area = 0; area < count; area++)
            {
                if (Fits(swing, area, origin, reach) && Visible(swing, area, eye))
                    Candidates.Add(area);
            }
            return Candidates.Count == 0 ? -1 : Candidates[Random.Range(0, Candidates.Count)];
        }

        /// <summary>
        /// A chunk still standing on this machine: health left and its collider active. The game hides a broken
        /// chunk's collider on every machine as soon as the owner's RPC_SetAreaHealth arrives, even while this machine's
        /// other healths lag.
        /// </summary>
        public static bool IsStanding(Rock rock, int area)
        {
            Collider collider = RockChunks.ColliderOf(rock, area);
            return collider != null && collider.gameObject.activeInHierarchy && RockChunks.IsIntact(rock, area);
        }

        /// <summary>How far the equipped weapon's swing reaches, with slack, and where it starts.</summary>
        private static float SwingReach(Player player, out Vector3 origin)
        {
            Attack attack = player.GetCurrentWeapon()?.m_shared?.m_attack;
            bool known = attack != null && attack.m_attackRange > 0f;
            origin = player.transform.position + Vector3.up * (known ? attack.m_attackHeight : FallbackHeight);
            return (known ? attack.m_attackRange : FallbackReach) + ReachSlack;
        }

        private static bool Fits(SeamNote swing, int area, Vector3 origin, float reach)
        {
            if (!IsStanding(swing.Rock, area) || !Outlasts(swing, area))
                return false;
            return RockChunks.ColliderOf(swing.Rock, area).bounds.SqrDistance(origin) <= reach * reach;
        }

        /// <summary>
        /// The chunk is left standing once the swing's damage is counted. The rock's owner has counted it already: the
        /// game applies a hit on its owner at once, and hides a chunk that broke. Any other machine has not, so it takes
        /// the swing's damage off the health the owner last saved in the ZDO.
        /// </summary>
        private static bool Outlasts(SeamNote swing, int area)
        {
            float damage = swing.DamageTo(area);
            return damage <= 0f || swing.Rock.IsOwner || RockChunks.SavedHealth(swing.Rock, area) > damage;
        }

        private static bool Visible(SeamNote swing, int area, Vector3 eye) =>
            swing.Areas.Contains(area) || InSight(RockChunks.ColliderOf(swing.Rock, area), eye);

        private static bool InSight(Collider collider, Vector3 eye)
        {
            Vector3 toCentre = collider.bounds.center - eye;
            float distance = toCentre.magnitude;
            if (distance < 0.01f)
                return false;
            return Physics.Raycast(eye, toCentre / distance, out RaycastHit hit, distance, SightMask(), QueryTriggerInteraction.Ignore)
                && hit.collider == collider;
        }

        /// <summary>The layers the game itself tests rock chunks against (MineRock5's own mask), the ground included.</summary>
        private static int SightMask()
        {
            if (sightMask == 0)
                sightMask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");
            return sightMask;
        }
    }
}

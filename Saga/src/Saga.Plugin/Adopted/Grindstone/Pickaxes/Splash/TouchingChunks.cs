// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Splash/TouchingChunks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The chunks touching a chunk of a MineRock5, on the rock's ZDO owner, found as the game's support test finds what
    /// holds a chunk up (MineRock5.UpdateSupport): Physics.OverlapBox with the chunk's support box (HitArea.m_bound: its
    /// collider's world bounds as SetupColliders took them, the centre relative to the rock's position, half the size, no
    /// rotation) on the game's own layer mask (MineRock5.m_rayMask). Only the rock's own intact chunks count
    /// (MineRock5.GetAreaIndex finds the collider): the ground, other rocks and everything else in the box are ignored,
    /// and so is the chunk itself.
    /// <para><b>Boxes filled before the hit.</b> The game fills every box once, on the first support check, from each
    /// collider's bounds; a broken chunk's collider object is inactive and its bounds are empty. A hit that breaks its
    /// chunk runs that first check itself, after the break, so the broken chunk would get an empty box and splash nothing.
    /// A prefix on MineRock5.RPC_Damage (<see cref="BoxesBeforeHit"/>) therefore fills the boxes before a Pickaxes hit is
    /// applied, while its chunk is still whole. That changes nothing for the game: its support check only ever uses the
    /// boxes of intact chunks, and those are the same whenever they are taken.</para>
    /// </summary>
    public static class TouchingChunks
    {
        /// <summary>The overlap results; the game's own buffer holds 128, the ground and small objects included.</summary>
        private static readonly Collider[] Found = new Collider[256];

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.RPC_Damage))]
        private static class BoxesBeforeHit
        {
            [HarmonyPrefix]
            private static void Prefix(MineRock5 __instance, HitData hit)
            {
                if (hit != null && hit.m_skill == PickSkill.Skill && PickSkill.Active && !__instance.m_haveSetupBounds)
                    HookGuard.Run("splash boxes", static rock => BeforeHit(rock), __instance);
            }
        }

        /// <summary>
        /// The area indexes of the intact chunks touching chunk <paramref name="area"/>, in index order; empty when there
        /// are none or the chunk has no box. Reloads the healths from the ZDO first, as the game does before damage.
        /// </summary>
        public static List<int> Of(MineRock5 chunks, int area)
        {
            var touching = new List<int>();
            if (chunks.m_hitAreas == null || area < 0 || area >= chunks.m_hitAreas.Count)
                return touching;
            SetUp(chunks);
            chunks.LoadHealth();
            var box = chunks.m_hitAreas[area].m_bound;
            if (box.m_size == Vector3.zero)
                return touching;
            Vector3 centre = chunks.transform.position + box.m_pos;
            int count = Physics.OverlapBoxNonAlloc(centre, box.m_size, Found, box.m_rot, MineRock5.m_rayMask);
            for (int i = 0; i < count; i++)
                AddChunk(chunks, area, Found[i], touching);
            Array.Clear(Found, 0, count);
            touching.Sort();
            return touching;
        }

        /// <summary>Fills the rock's support boxes the way the game's first support check would, once per rock instance.</summary>
        private static void SetUp(MineRock5 chunks)
        {
            if (chunks.m_haveSetupBounds)
                return;
            chunks.SetupColliders();
            chunks.m_haveSetupBounds = true;
        }

        /// <summary>On the owner, before a Pickaxes hit that can splash is applied: fills the boxes while its chunk is whole.</summary>
        private static void BeforeHit(MineRock5 chunks)
        {
            ZNetView view = chunks.m_nview;
            if (chunks.m_hitAreas == null || view == null || !view.IsValid() || !view.IsOwner())
                return;
            if (PickaxePerkSettings.SplashDamage.Value > 0f)
                SetUp(chunks);
        }

        /// <summary>Adds <paramref name="collider"/>'s chunk when it is one of the rock's intact chunks other than the hit one.</summary>
        private static void AddChunk(MineRock5 chunks, int area, Collider collider, List<int> touching)
        {
            int index = collider != null ? chunks.GetAreaIndex(collider) : -1;
            if (index < 0 || index == area || touching.Contains(index))
                return;
            if (chunks.m_hitAreas[index].m_health > 0f)
                touching.Add(index);
        }
    }
}

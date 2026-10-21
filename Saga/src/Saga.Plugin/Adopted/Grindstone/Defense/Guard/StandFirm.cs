// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Guard/StandFirm.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Less knockback from blocked hits. Character.RPC_Damage pushes the player back with the hit's m_pushForce right
    /// after Humanoid.BlockAttack, which already shrinks it for a held block; the block hook shrinks it further by
    /// Knockback Reduction, on the blocker's own client.
    /// </summary>
    public static class StandFirm
    {
        public static void OnBlock(HitData hit) =>
            hit.m_pushForce *= 1f - Mathf.Clamp01(DefenseSkill.LocalShare(DefenseGuardSettings.Knockback.Value));
    }
}

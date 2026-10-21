// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Guard/ShieldBash.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Shield Bash, the "stagger chance": an ordinary block (not a parry, which already staggers) with a shield, whose
    /// guard held, staggers the attacker with Shield Bash Chance. It uses the game's own parry stagger,
    /// Character.Stagger, which reaches the attacker's owner by RPC, and only for attackers the game staggers on a parry
    /// (m_staggerWhenBlocked). Rolled on the blocker's client.
    /// </summary>
    public static class ShieldBash
    {
        public static void OnBlock(Character attacker, HitData hit)
        {
            if (attacker == null || attacker.IsPlayer() || !attacker.m_staggerWhenBlocked || attacker.IsStaggering())
                return;
            if (Random.value >= DefenseSkill.LocalShare(DefenseGuardSettings.BashChance.Value))
                return;
            attacker.Stagger(-hit.m_dir);
            DefenseCallout.Show(attacker.GetTopPoint(), SagaText.Text("gs_f03e7eabeab30d55"));
        }
    }
}

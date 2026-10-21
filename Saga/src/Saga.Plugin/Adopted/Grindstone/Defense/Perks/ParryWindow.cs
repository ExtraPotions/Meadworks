// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Perks/ParryWindow.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The parry window: the game's 0.25 seconds after raising a block, plus Parry Window At 100 at the local player's
    /// level. <see cref="BlockState"/> applies it inside Humanoid.BlockAttack.
    /// </summary>
    public static class ParryWindow
    {
        /// <summary>The local player's parry window in seconds; the game's own while Defense is off.</summary>
        public static float Local()
        {
            float extra = DefenseSkill.Active ? Mathf.Max(0f, DefenseSettings.ParryWindow.Value) * DefenseSkill.Factor(DefenseSkill.Local()) : 0f;
            return BlockState.GameParryWindow + extra;
        }
    }
}

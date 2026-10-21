// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Milestones/Hardened.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Hardened, the level 75 milestone, on the local player's own client: each hit from a creature (or a training PvP
    /// hit) that hurts without being blocked adds a stack, up to Hardened Max Stacks; each stack takes Hardened Per
    /// Stack percent off the damage taken (<see cref="Reductions"/>). The stacks last Hardened Duration seconds after the
    /// last hit that added one. The hit that adds a stack is not reduced by it. An icon shows the stacks
    /// (<see cref="DefenseEffects"/>). The stacks are kept in memory only.
    /// </summary>
    public static class Hardened
    {
        public static void Reset() { stacks = 0; until = float.NegativeInfinity; }
        private static int stacks;
        private static float until = float.NegativeInfinity;

        public static int Stacks => Time.time <= until ? stacks : 0;

        public static float Remaining => Stacks > 0 ? until - Time.time : 0f;

        public static float Reduction() => Stacks * Mathf.Max(0f, DefenseMilestoneSettings.HardenedPerStack.Value) / 100f;

        public static void AddStack()
        {
            if (!DefenseSkill.LocalReached(DefenseMilestoneSettings.HardenedLevel.Value))
                return;
            stacks = Mathf.Min(Stacks + 1, Mathf.Max(1, DefenseMilestoneSettings.HardenedMaxStacks.Value));
            until = Time.time + DefenseMilestoneSettings.HardenedDuration.Value;
            DefenseEffects.Refresh();
        }
    }
}

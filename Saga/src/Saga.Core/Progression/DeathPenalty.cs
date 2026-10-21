// Saga - ExtraPotions Meadworks progression.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
//
// Portions Copyright (C) 2026 Gerald Glasgow, GrindstoneSkills 0.14.1, GPL-3.0-or-later.
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1:
// GrindstoneSkills/GrindstoneSkills/Skills/DeathPenalty.cs.
// ValheimMods - Valheim BepInEx mods and shared libraries. Copyright (C) 2026 Gerald Glasgow. This program is free
// software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published
// by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. See
// THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
//
// Changed by expDARE, 2026: namespace Saga.Progression; Unity-free; the decision (skip, factor, keep progress) and the
// game's LowerAllSkills arithmetic as pure functions; the Harmony prefix, finalizer and accumulator restore stay in the
// plugin.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Saga.Progression
{
    /// <summary>What the dying player's client does in <c>Skills.OnDeath</c>.</summary>
    public readonly struct DeathPlan
    {
        public DeathPlan(bool skip, float lowerFactor, bool keepProgress)
        {
            Skip = skip;
            LowerFactor = lowerFactor;
            KeepProgress = keepProgress;
        }

        /// <summary>No loss and progress kept: the game's call (and its "skills lowered" message) is skipped.</summary>
        public bool Skip { get; }

        /// <summary>The value <c>m_DeathLowerFactor</c> takes for the call; the game multiplies it by the world's skill reduction rate.</summary>
        public float LowerFactor { get; }

        /// <summary>Every skill's progress toward its next level is put back after <c>LowerAllSkills</c>.</summary>
        public bool KeepProgress { get; }
    }

    public static class DeathPenalty
    {
        /// <summary>The plan for Skill Loss On Death (percent, 0 to 100) and Lose Progress On Death.</summary>
        public static DeathPlan Plan(float lossPercent, bool loseProgress)
        {
            var loss = float.IsNaN(lossPercent) ? 0f : Math.Min(100f, Math.Max(0f, lossPercent)) / 100f;
            var keep = !loseProgress;
            return new DeathPlan(loss <= 0f && keep, loss, keep);
        }

        /// <summary>
        /// One skill after the death: the game's <c>LowerAllSkills(factor)</c> takes <c>level * factor</c> off and zeroes the
        /// accumulator, with factor = <see cref="DeathPlan.LowerFactor"/> times the world's skill reduction rate; keep
        /// progress puts the accumulator back.
        /// </summary>
        public static (float Level, float Accumulator) Apply(float level, float accumulator, DeathPlan plan, float worldReductionRate)
        {
            if (plan.Skip) return (level, accumulator);
            var factor = plan.LowerFactor * Math.Max(0f, worldReductionRate);
            return (level - level * factor, plan.KeepProgress ? accumulator : 0f);
        }
    }
}

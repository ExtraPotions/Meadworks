// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Summoner: each time the boss has lost another `every` percent of its maximum health, it calls a wave. The waves
    /// already called are counted in the boss's ZDO (see <see cref="HealthStepBehaviour"/> for the rules every mark
    /// follows).
    /// </summary>
    public sealed class SummonerBehaviour : HealthStepBehaviour
    {
        /// <summary>How many whole `every` steps of health have been lost; the epsilon keeps 67% of 100 from reading 0.9999.</summary>
        protected override int Passed(float fraction)
        {
            float step = Mathf.Clamp(AspectMath.Power(Aspect.Summoner, Fields.Every), 1f, 100f) / 100f;
            return Mathf.FloorToInt((1f - fraction) / step + 0.0001f);
        }

        protected override int Counted(ZDO zdo) => AspectStore.GetWaves(zdo);

        protected override void Count(ZDO zdo, int passed) => AspectStore.SetWaves(zdo, passed);

        protected override void Fire() => SummonWave.Call(Character, Controller);
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Phantom: the boss splits off its copies each time its health falls past one of the `split at` marks - 66% and
    /// 33% of its maximum by default - and each split brings one copy per player online (<see cref="PhantomSpawner"/>).
    /// The splits already made are counted in the boss's ZDO (see <see cref="HealthStepBehaviour"/> for the rules every
    /// mark follows). A mark outside 0-100 never fires.
    /// </summary>
    public sealed class PhantomBehaviour : HealthStepBehaviour
    {
        /// <summary>So a boss at exactly 66% reads as having reached the 66 mark despite float rounding.</summary>
        private const float Epsilon = 0.001f;

        protected override int Passed(float fraction)
        {
            float percent = fraction * 100f;
            int passed = 0;
            foreach (float mark in AspectMath.PhantomSplits())
            {
                if (mark > 0f && mark <= 100f && percent <= mark + Epsilon)
                {
                    passed++;
                }
            }
            return passed;
        }

        protected override int Counted(ZDO zdo) => AspectStore.GetSplits(zdo);

        protected override void Count(ZDO zdo, int passed) => AspectStore.SetSplits(zdo, passed);

        protected override void Fire() => PhantomSpawner.Spawn(Controller);
    }
}

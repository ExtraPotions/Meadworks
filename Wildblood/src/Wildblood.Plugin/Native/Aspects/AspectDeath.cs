// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The aspect work that must happen at the instant of a boss's death, from the death patch's prefix on its owner,
    /// because it needs the dying boss's ZDO and network view, both gone a moment later: a Twin tells its partner to fall
    /// with it, and a Phantom copy sends its vanishing puff to every client holding it (it leaves no body to mark the
    /// spot). Exception-safe and never rethrows: nothing here may stop a creature from dying.
    /// </summary>
    internal static class AspectDeath
    {
        public static void Capture(Character victim, EliteController controller, ZNetView nview)
        {
            try
            {
                Act(victim, controller.Traits, nview);
            }
            catch (Exception e)
            {
                Guard.Report(e, "boss aspect death");
            }
        }

        private static void Act(Character victim, CreatureTraits traits, ZNetView nview)
        {
            if (traits.PhantomCopy)
            {
                CreatureRpc.FireFlash(nview, victim.GetCenterPoint(), 2f, "phantom");
            }
            else if (traits.HasAspect(Aspect.Twin))
            {
                TwinLink.SendFall(nview.GetZDO());
            }
        }
    }
}

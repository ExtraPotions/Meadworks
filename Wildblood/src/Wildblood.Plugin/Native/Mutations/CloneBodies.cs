// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// A Cloning creature and its decoy are made in the same place, so their bodies would shove each other apart the
    /// moment the decoy appears and give the trick away. Each machine tells its own physics to let the two pass through
    /// each other: the decoy's owner as it makes it, and every machine as the decoy wakes there, so whichever machine
    /// later owns either body already ignores the other. Local and harmless: it touches only this pair.
    /// </summary>
    internal static class CloneBodies
    {
        public static void Ignore(Character? real, Character? decoy)
        {
            if (real == null || decoy == null)
            {
                return;
            }
            Collider[] decoyParts = decoy.GetComponentsInChildren<Collider>(true);
            foreach (Collider part in real.GetComponentsInChildren<Collider>(true))
            {
                foreach (Collider other in decoyParts)
                {
                    if (part != null && other != null)
                    {
                        Physics.IgnoreCollision(part, other);
                    }
                }
            }
        }
    }
}

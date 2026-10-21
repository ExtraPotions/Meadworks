// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Suppresses a creature's nameplate while its body is hidden on this machine, in step with its renderers, so it gives
    /// nothing away - not even a nameplate: a Cloaked creature beyond its visible distance, a Blinking one for the moment
    /// its body is veiled mid-blink, so the plate never lingers at the spot it left, and a Cloning one while it hides
    /// behind its decoy (<see cref="CloneBehaviour.Hidden"/>), so only the decoy's plate shows and nothing marks where the
    /// creature really is; the decoy's own plate reads the same name, stars and health. The game asks this for every
    /// creature near the player each frame, so the hidden ones are found in one lookup (<see cref="PlateVeils"/>), and
    /// with none loaded it is one count check.
    /// </summary>
    [HarmonyPatch(typeof(EnemyHud), "TestShow")]
    public static class EnemyHudCloakPatch
    {
        private static void Postfix(Character c, ref bool __result)
        {
            if (EnemyHudPatch.Available && __result && c != null && (PlateVeils.Hides(c) || EchoLink.IsEcho(c)))
            {
                __result = false;
            }
        }
    }
}

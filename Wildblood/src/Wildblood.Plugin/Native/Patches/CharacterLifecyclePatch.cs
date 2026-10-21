// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Horde adoption adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543.
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration; creatures on the Protected Prefabs list get no controller.
using ExtraPotions.Wildblood.Adopted.Aspects;
using ExtraPotions.Wildblood.Adopted.Breeding;
using ExtraPotions.Wildblood.Adopted.Runtime;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Gives every non-player creature an <see cref="EliteController"/> as it wakes. The controller does the rest on
    /// its own first frame - by then any spawner has already set the vanilla star level, so nothing is lost. Players
    /// are filtered inside the controller, and a boss resolves there on the boss table, so this stays a cheap attach.
    /// </summary>
    [HarmonyPatch(typeof(Character), "Awake")]
    public static class CharacterLifecyclePatch
    {
        private static void Postfix(Character __instance) =>
            Guard.Run("Character.Awake attach", static character => Attach(character), __instance);

        private static void Attach(Character character)
        {
            ExtraPotions.Wildblood.Adopted.Horde.WildSpawn.Notice(character);
            if (character.IsPlayer() || character.GetComponent<EliteController>() != null)
            {
                return;
            }
            // Protected Prefabs are never starred, traited or scaled: no controller, so the game's own level and stats stand.
            if (ExtraPotions.Wildblood.Runtime.ProtectedCreatures.IsProtected(character.gameObject))
            {
                return;
            }
            EliteController controller = character.gameObject.AddComponent<EliteController>();
            EchoBody.VeilIfEcho(character); // an Echoing boss's echo, waking from its ZDO here: veiled before it is ever drawn
            if (character.IsBoss())
            {
                AltarSummon.Claim(controller); // a boss an altar is instantiating right now takes the aspect it offered for
            }
            else
            {
                Lineage.Claim(character); // carry after the game supplies the native newborn level; Shepherd owns its active module
            }
        }
    }
}

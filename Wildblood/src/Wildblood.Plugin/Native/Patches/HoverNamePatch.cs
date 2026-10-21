// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Display;
using ExtraPotions.Wildblood.Adopted.Runtime;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Prepends a creature's mutation words to its hover name. Vanilla's GetHoverName already returns the localized
    /// creature name, so the result is decorated once more with the mutation words; the nameplate re-localizes it,
    /// which is a no-op for plain text. This makes the name the complete, authoritative tell everywhere it appears. The
    /// game asks for it every frame a nameplate shows, so each creature keeps its decorated name
    /// (<see cref="EliteController.Name"/>) and builds it again only when the name it decorates changes.
    /// </summary>
    [HarmonyPatch(typeof(Character), "GetHoverName")]
    public static class HoverNamePatch
    {
        /// <summary>True while <see cref="Plain"/> reads the undecorated name.</summary>
        private static bool bypass;

        /// <summary>The creature's hover name without trait words, as other mods and the game make it.</summary>
        internal static string Plain(Character character)
        {
            bypass = true;
            try { return character.GetHoverName(); }
            finally { bypass = false; }
        }

        private static void Postfix(Character __instance, ref string __result)
        {
            if (bypass || TraitLine.ShowsPlainName(__instance)) return; // the trait words sit on their own line above the HUD name
            if (EnemyHudPatch.Available && Configuration.ShowTraitNames.Value)
            {
                __result = Guard.Run("Character.GetHoverName", static (character, name) => Decorate(character, name),
                    __instance, __result);
            }
        }

        private static string Decorate(Character character, string name)
        {
            EliteController? controller = ReadyElites.Of(character);
            if (controller == null || !Within(character)
                || Disguise.Holds(character)) // a dormant mimic keeps the chest's name, undecorated
            {
                return name;
            }
            return controller.Name.Decorate(controller.Traits, name);
        }

        internal static bool Within(Character character)
        {
            float limit = Config.Configuration.NameplateDistance.Value;
            Player local = Player.m_localPlayer;
            if (limit <= 0f || local == null)
            {
                return true;
            }
            return (local.transform.position - character.transform.position).sqrMagnitude <= limit * limit;
        }
    }
}

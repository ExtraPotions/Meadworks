// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Fishing/Core/FloatSetup.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A cast lands: FishingFloat.Setup runs on the caster's client, which made the float and owns it. For the local
    /// player's cast the postfix stamps the angler's level on the float's ZDO (<see cref="Angler"/>), attaches the cast's
    /// state (<see cref="FloatFight"/>) and lengthens the line (<see cref="Tackle"/>).
    /// </summary>
    public static class FloatSetup
    {
        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Setup))]
        private static class Landed
        {
            [HarmonyPostfix]
            private static void Postfix(FishingFloat __instance, Character owner)
            {
                if (FishSkill.Active && owner != null && owner == Player.m_localPlayer && __instance.m_nview.IsValid())
                    HookGuard.Run("fishing float setup", () => Setup(__instance, (Player)owner));
            }
        }

        private static void Setup(FishingFloat fishingFloat, Player angler)
        {
            float level = FishSkill.Of(angler);
            Angler.Stamp(fishingFloat, level);
            FloatFight.Attach(fishingFloat, level);
            Tackle.OnLanded(fishingFloat, level);
        }
    }
}

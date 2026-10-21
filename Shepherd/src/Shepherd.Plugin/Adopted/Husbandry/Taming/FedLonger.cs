// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Taming/FedLonger.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Crucible boundaries; owned native lifecycle.
// SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Stay fed longer. When a creature eats, on its owner, <c>Tameable.OnConsumedItem</c> stamps the feeding time in the
    /// ZDO, and the creature counts as hungry once more than m_fedDuration (600 s) has passed since that stamp
    /// (<c>IsHungry</c>, read by taming, breeding, healing and the hover). A postfix moves the stamp into the future by
    /// m_fedDuration times the Fed Duration share of the best keeper within Keeper Range, so every reader of
    /// <c>IsHungry</c> agrees without a patch of its own. The bonus is fixed at the moment of eating.
    /// </summary>
    public static class FedLonger
    {
        [HarmonyPatch(typeof(Tameable), nameof(Tameable.OnConsumedItem))]
        private static class Ate
        {
            [HarmonyPostfix]
            private static void Postfix(Tameable __instance) => NativeHusbandry.Run("fed longer", () => Extend(__instance));
        }

        private static void Extend(Tameable tameable)
        {
            ZNetView nview = tameable.m_nview;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;
            float extra = tameable.m_fedDuration * Keeper.Share(HusbandryTamingSettings.FedDuration.Value, tameable.transform.position);
            if (extra > 0f)
                nview.GetZDO().Set(ZDOVars.s_tameLastFeeding, Herd.TicksIn(extra));
        }
    }
}

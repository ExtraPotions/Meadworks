// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Fishing/Bites/BiteChance.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Whether a fish goes for a float. Every time a fish picks a new place to swim (Fish.RandomizeWaypoint, on the fish's
    /// owner, which may be any machine near it) it looks at every float in the water within the float's range (50 m)
    /// that has no fish on it, and goes for each with its base hook chance (10% for every fish in the game); then it swims
    /// to the float and nibbles. The prefix replaces that look (Fish.FindFloat) with the same one, where the chance for
    /// a float is multiplied by:
    /// <list type="bullet">
    /// <item>the angler's level (Bite Chance At 100), read from the float's ZDO (<see cref="Angler"/>);</item>
    /// <item>dawn and dusk, and rain (<see cref="FishingConditions"/>);</item>
    /// <item>chum floating near the float (<see cref="Chum"/>).</item>
    /// </list>
    /// A legendary fish ignores the floats of anglers below Legendary Level. Whether the bait is right is still the
    /// game's test, at the nibble.
    /// </summary>
    public static class BiteChance
    {
        [HarmonyPatch(typeof(Fish), nameof(Fish.FindFloat))]
        private static class Choose
        {
            [HarmonyPrefix]
            private static bool Prefix(Fish __instance, ref FishingFloat __result, bool __runOriginal)
            {
                if (!__runOriginal)
                    return false;
                if (!FishSkill.Active)
                    return true;
                (bool handled, FishingFloat picked) bite =
                    HookGuard.Run("bite chance", static fish => (true, Pick(fish)), __instance, (false, (FishingFloat)null));
                if (bite.handled)
                    __result = bite.picked;
                return !bite.handled;
            }
        }

        /// <summary>The chance, 0..1, that this fish goes for this float when it next picks where to swim.</summary>
        public static float Chance(Fish fish, FishingFloat fishingFloat) =>
            Mathf.Clamp01(fish.m_baseHookChance * Factor(fishingFloat));

        private static FishingFloat Pick(Fish fish)
        {
            bool legendary = FishInfo.IsLegendary(FishInfo.Level(fish));
            Vector3 position = fish.transform.position;
            foreach (FishingFloat fishingFloat in FishingFloat.GetAllInstances())
            {
                if (!Reachable(fishingFloat, position))
                    continue;
                if (legendary && !FishSkill.Reached(Angler.Level(fishingFloat), FishingBigFishSettings.LegendaryLevel.Value))
                    continue;
                if (Random.value < Chance(fish, fishingFloat))
                    return fishingFloat;
            }
            return null;
        }

        /// <summary>The game's own test: the float is in the water, within its range, with no fish on it.</summary>
        private static bool Reachable(FishingFloat fishingFloat, Vector3 position) =>
            fishingFloat != null && fishingFloat.IsInWater()
            && Vector3.Distance(position, fishingFloat.transform.position) <= fishingFloat.m_range
            && fishingFloat.GetCatch() == null;

        private static float Factor(FishingFloat fishingFloat)
        {
            float skill = 1f + FishSkill.Share(FishingBiteSettings.BiteChanceAt100.Value, Angler.Level(fishingFloat));
            return skill * FishingConditions.BiteFactor() * Chum.Factor(fishingFloat.transform.position);
        }
    }
}

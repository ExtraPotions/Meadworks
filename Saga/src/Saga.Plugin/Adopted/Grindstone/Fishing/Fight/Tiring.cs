// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Fight/Tiring.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A hooked fish tires. The game starts a thrash with Fish.Escape on the fish's owner, which after the hook is the
    /// angler's client (OnHooked claims the fish): once when hooked, then 1.5 to 5 s after the last one ended (1.25 to 4 s
    /// for later fish). It lasts 0.5 to 3 s (1 to 4 s for later fish) plus 1.5 s per level, and every client sees it
    /// through the fish's ZDO (the escape key). For a
    /// fish on the local player's float the postfix:
    /// <list type="bullet">
    /// <item>cancels the thrash the hook starts after a perfect strike (<see cref="Strike"/>);</item>
    /// <item>shortens each thrash by Tiring Per Thrash for every thrash before it, down to 20%;</item>
    /// <item>once the fish has thrashed Thrashes To Tire times (legendary fish: Legendary Thrashes), cancels every further
    /// thrash: the fish is spent, "Spent!" floats up and the line comes in faster (<see cref="FloatScope"/>).</item>
    /// </list>
    /// A cancelled thrash also waits the game's usual pause before the next try, as a finished one would.
    /// </summary>
    public static class Tiring
    {
        private const float MinShare = 0.2f;
        private const float RestingEscapeTime = -0.01f;

        [HarmonyPatch(typeof(Fish), nameof(Fish.Escape))]
        private static class Thrash
        {
            [HarmonyPostfix]
            private static void Postfix(Fish __instance)
            {
                if (FishSkill.Active)
                    HookGuard.Run("thrash", static fish => OnThrash(fish), __instance);
            }
        }

        /// <summary>How many thrashes tire this fish out; 0 when it never tires.</summary>
        public static int Limit(Fish fish) =>
            FishInfo.IsLegendary(FishInfo.Level(fish)) ? FishingBigFishSettings.LegendaryThrashes.Value : FishingFightSettings.ThrashesToTire.Value;

        private static void OnThrash(Fish fish)
        {
            if (!fish.IsHooked() || fish.m_nview == null || !fish.m_nview.IsValid() || !fish.m_nview.IsOwner())
                return;
            FloatFight fight = FloatFight.Of(FishingFloat.FindFloat(fish));
            if (fight == null || fight.Fish != fish)
                return;
            if (fight.PerfectPending)
            {
                fight.PerfectPending = false;
                Calm(fish);
                return;
            }
            int limit = Limit(fish);
            if (limit > 0 && fight.Thrashes >= limit)
            {
                Spend(fight, fish);
                return;
            }
            Shorten(fish, Mathf.Max(MinShare, 1f - FishSkill.Percent(FishingFightSettings.TiringPerThrash.Value) * fight.Thrashes));
            fight.Thrashes++;
        }

        private static void Spend(FloatFight fight, Fish fish)
        {
            Calm(fish);
            if (fight.Spent)
                return;
            fight.Spent = true;
            FishCallout.ShowLocal(fish.transform.position + Vector3.up, SagaText.Text("gs_f541094d518d6567"));
        }

        private static void Shorten(Fish fish, float share)
        {
            fish.m_escapeTime *= share;
            fish.m_nview.GetZDO().Set(ZDOVars.s_escape, fish.m_escapeTime);
        }

        /// <summary>
        /// Ends the thrash before it starts, and waits the game's pause before the next try. The game's own rest after a
        /// thrash is an escape time just below 0 (it counts down past 0): only then does a hooked fish stop swimming and
        /// hang on the line (Fish.CustomFixedUpdate), so a calmed fish gets the same.
        /// </summary>
        private static void Calm(Fish fish)
        {
            fish.m_escapeTime = RestingEscapeTime;
            fish.m_nview.GetZDO().Set(ZDOVars.s_escape, 0f);
            fish.m_nextEscape = Time.time + Random.Range(fish.m_escapeWaitMin, fish.m_escapeWaitMax);
        }
    }
}

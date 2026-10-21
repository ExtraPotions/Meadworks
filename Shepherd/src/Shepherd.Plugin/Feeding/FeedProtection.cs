// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using HarmonyLib;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    [HarmonyPatch(typeof(WearNTear), "RPC_Damage", typeof(long), typeof(HitData))]
    internal static class FeedProtection
    {
        [HarmonyPrefix]
        static bool Prefix(WearNTear __instance, HitData hit) => !NativeFeeding.Run("feed container protection",
            static call => Protects(call.wear, call.hit), (wear: __instance, hit), false);

        static bool Protects(WearNTear wear, HitData hit)
        {
            if (!NativeFeeding.FeedingActive || !ShepherdSettings.ProtectContainers.Value || wear == null || hit == null
                || wear.m_nview == null || !wear.m_nview.IsValid() || !wear.m_nview.IsOwner()) return false;
            var attacker = hit.GetAttacker();
            if (attacker == null || attacker.IsPlayer() || attacker.IsDead()) return false;
            var animal = attacker.GetComponent<MonsterAI>();
            if (animal == null || (animal.m_tamable == null && !YoungFeeding.Supports(animal)) || animal.m_consumeItems == null || animal.m_consumeItems.Count == 0
                || NativeFeeding.AnimalDisallowed(Herd.PrefabName(attacker))) return false;
            if (!attacker.IsTamed() && !Herd.IsBeingTamed(animal.m_tamable) && !ShepherdSettings.FeedWild.Value) return false;
            foreach (var source in wear.GetComponentsInChildren<Container>(true))
            {
                if (source.m_nview != wear.m_nview || !FeedSources.IsUsable(source)) continue;
                if (TroughPieces.IsTrough(source)
                    || (ShepherdSettings.ContainerFeeding.Value && FeedSources.IsFeedContainer(source))) return true;
            }
            return false;
        }
    }
}

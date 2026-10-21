// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace ExtraPotions.Ember.Runtime
{
    [HarmonyPatch(typeof(Bed), "CheckEnemies")]
    internal static class BedEnemyWarning
    {
        // The native sensed timer outlives its source. Only complete local AI evidence can clear that residual.
        internal static bool ConfirmedStale(Player human)
        {
            if (!human || human != Player.m_localPlayer || human.IsDead() || !EmberModules.ProductActive(out _)
                || !human.IsSensed()) return false;
            try
            {
                var playerView = human.GetComponent<ZNetView>();
                if (!playerView || !playerView.IsValid() || !playerView.IsOwner()) return false;
                var instances = BaseAI.GetAllInstances();
                if (instances == null) return false;
                foreach (var ai in instances)
                {
                    if (!ai) continue;
                    var character = ai.GetComponent<Character>();
                    if (!character) return false;
                    if (character.IsDead() || !BaseAI.IsEnemy(character, human)) continue;
                    // Native AnimalAI passes sensed:false while fleeing; it does not renew this timer.
                    if (ai.GetType() == typeof(AnimalAI)) continue;
                    var view = ai.GetComponent<ZNetView>();
                    if (!view || !view.IsValid() || !view.IsOwner()) return false;
                    if (ai.GetType() != typeof(MonsterAI)) return false;
                    if (!ai.enabled || !ai.gameObject.activeInHierarchy) continue;
                    if (ai.GetTargetCreature() == human && (ai.CanSeeTarget(human) || ai.CanHearTarget(human))) return false;
                }
                return true;
            }
            catch
            {
                // Missing or changed native evidence keeps the native refusal and warning.
                return false;
            }
        }

        [HarmonyPrefix]
        internal static bool Prefix(Player human, ref bool __result, bool __runOriginal)
        {
            if (!__runOriginal || !ConfirmedStale(human)) return true;
            __result = true;
            return false;
        }
    }
}

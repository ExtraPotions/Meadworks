// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using HarmonyLib;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The game's AI steps aside, on the creature's owner: for the Executioner while its rear strike turns it (else it
    /// would turn it back towards its target, <see cref="HeadsmanBrain"/>), and for a skeleton it raises while its bones
    /// are still forming (<see cref="HeadsmanRising"/>).
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    public static class HeadsmanAiPatch
    {
        /// <summary>The AIs that may have to wait (the Executioners and the skeletons they raise), each with its question.</summary>
        private static readonly Dictionary<MonsterAI, System.Func<bool>> waiting = new Dictionary<MonsterAI, System.Func<bool>>();

        public static void Watch(MonsterAI? ai, System.Func<bool> waits)
        {
            if (ai != null)
            {
                waiting[ai] = waits;
            }
        }

        public static void Forget(MonsterAI? ai)
        {
            if (ai != null)
            {
                waiting.Remove(ai);
            }
        }

        private static bool Prefix(MonsterAI __instance, ref bool __result)
        {
            if (waiting.Count == 0 || !waiting.TryGetValue(__instance, out System.Func<bool> waits) || !waits())
            {
                return true;
            }
            __instance.StopMoving();
            __result = true;
            return false;
        }
    }

    /// <summary>
    /// The rear strike only when two or more foes are near (its target is behind, which its attack's inverted angle
    /// check already asks): the user's "several targets and one behind".
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanUseAttack))]
    public static class HeadsmanRear
    {
        private const float Near = 8f;
        private static readonly List<Character> around = new List<Character>();

        /// <summary>Every creature's every attack passes here: only the rear strike (the Executioner's alone) goes further.</summary>
        private static void Postfix(BaseAI __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (__result && item?.m_shared.m_name == HeadsmanAttacks.RearName)
            {
                __result = SafeCall.Run("BaseAI.CanUseAttack rear strike", static ai => Foes(ai.m_character) >= 2, __instance, true);
            }
        }

        private static int Foes(Character? boss)
        {
            if (boss == null)
            {
                return 0;
            }
            around.Clear();
            Character.GetCharactersInRange(boss.transform.position, Near, around);
            int foes = 0;
            foreach (Character other in around)
            {
                if (other != boss && !other.IsDead() && BaseAI.IsEnemy(boss, other))
                {
                    foes++;
                }
            }
            around.Clear();
            return foes;
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Makes a Gilded creature run. On the owner - the only machine where the AI steps at all - a Gilded creature with a
    /// player in sight (<see cref="GildedBehaviour.FleeFrom"/>) spends the step fleeing instead of thinking: the base
    /// AI's upkeep runs (regeneration, timers, take-off and landing), then the game's own flee takes it to a reachable
    /// point away from the player, re-picked every couple of seconds, exactly as a creature flees fire. It is alerted
    /// first so it runs rather than walks. The MonsterAI's own step is skipped rather than overridden afterwards,
    /// because the AI caches its path for a second: a step that had already walked it somewhere would drag the flee
    /// along that same path. When no player is in sight the step is the game's, untouched - and even then it cannot
    /// pick a player to fight, because a Gilded creature has no player enemies (<see cref="GildedEnemyPatch"/>).
    /// Movement and the alert replicate through the game's own sync, so every client sees it bolt. For every other
    /// creature the cost is one static check, plus one dictionary lookup only while a Gilded creature is loaded. A
    /// failure is logged once and swallowed, and the step falls back to the game's: every creature's AI must still run.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    public static class GildedFleePatch
    {
        private static Func<BaseAI, float, bool>? _baseStep;
        private static Func<BaseAI, float, Vector3, bool>? _flee;
        private static Action<BaseAI, bool>? _alert;
        private static bool _reported;

        // A Harmony prefix: false skips the MonsterAI step (this step was the flee), true leaves it to the game.
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result)
        {
            if (!GildedBehaviour.Any)
            {
                return true; // no Gilded creature loaded: the common case, and free
            }
            GildedBehaviour? gilded = GildedBehaviour.For(__instance);
            return gilded == null || !SafeTakeOver(__instance, gilded, dt, ref __result);
        }

        private static bool SafeTakeOver(MonsterAI ai, GildedBehaviour gilded, float dt, ref bool result)
        {
            try
            {
                return TakeOver(ai, gilded, dt, ref result);
            }
            catch (Exception e) when (!_reported)
            {
                _reported = true;
                Guard.Report(e, "MonsterAI.UpdateAI gilded flee");
                return false;
            }
            catch (Exception)
            {
                return false; // already reported once this session
            }
        }

        /// <summary>True when this step was the flee; false leaves the step to the game.</summary>
        private static bool TakeOver(MonsterAI ai, GildedBehaviour gilded, float dt, ref bool result)
        {
            if (!gilded.FleeFrom(out Vector3 from))
            {
                return false;
            }
            _baseStep ??= Bind<Func<BaseAI, float, bool>>("UpdateAI", virtualCall: false, typeof(float));
            _alert ??= Bind<Action<BaseAI, bool>>("SetAlerted", virtualCall: true, typeof(bool));
            _flee ??= Bind<Func<BaseAI, float, Vector3, bool>>("Flee", virtualCall: false, typeof(float), typeof(Vector3));
            result = _baseStep(ai, dt); // BaseAI's own step, not MonsterAI's: the upkeep without the thinking
            if (result && !ai.IsAlerted())
            {
                _alert(ai, true); // MonsterAI's override, and the alert replicates to every client
            }
            if (result)
            {
                _flee(ai, dt, from);
            }
            return true;
        }

        /// <summary>An open delegate to one of BaseAI's own methods, bound once. Non-virtual reaches the base step
        /// past MonsterAI's override; virtual dispatches to the creature's override (MonsterAI's SetAlerted).</summary>
        private static T Bind<T>(string method, bool virtualCall, params Type[] parameters) where T : Delegate =>
            AccessTools.MethodDelegate<T>(AccessTools.Method(typeof(BaseAI), method, parameters), null, virtualCall);
    }
}

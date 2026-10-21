// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// A raider walking off (<see cref="RaiderLeave"/>) spends its AI step walking, not thinking: the base AI's upkeep
    /// runs (the owner test, timers, take-off and landing, regeneration), then the game's own flee takes it away from the
    /// base, and the monster's step - which would pick a player to fight - is skipped, as a Gilded creature's is while it
    /// runs (<see cref="Patches.GildedFleePatch"/>). Movement replicates through the game's own sync. The base step only
    /// moves anything on the owner, the one machine a raider walks off on. Every monster's AI step on this machine comes
    /// through here; with no raider walking off here - nearly always - one static read ends it, and only while one is, a
    /// dictionary lookup tells it apart. A failure is reported once and the step is left to the game.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    internal static class RaiderLeavePatch
    {
        private static Func<BaseAI, float, bool>? _baseStep;
        private static bool _reported;

        // false skips the monster's step (this step was the walk); true leaves it to the game. An earlier prefix that
        // already skipped the step (a Gilded creature fleeing) keeps it skipped.
        private static bool Prefix(MonsterAI __instance, float dt, ref bool __result, bool __runOriginal)
        {
            if (RaiderRoster.Leaving == 0 || !__runOriginal || !RaiderRoster.TryLeaver(__instance, out RaiderSteering raider))
            {
                return __runOriginal;
            }
            try
            {
                __result = BaseStep()(__instance, dt);
                if (__result)
                {
                    raider.Leave.Step(__instance, dt);
                }
                return false;
            }
            catch (Exception e)
            {
                ReportOnce(e);
                return true;
            }
        }

        // BaseAI's own step, not MonsterAI's: called through the virtual it would land back in MonsterAI.UpdateAI, here.
        private static Func<BaseAI, float, bool> BaseStep() =>
            _baseStep ??= AccessTools.MethodDelegate<Func<BaseAI, float, bool>>(
                AccessTools.Method(typeof(BaseAI), nameof(BaseAI.UpdateAI), new[] { typeof(float) }), null, false);

        private static void ReportOnce(Exception e)
        {
            if (!_reported)
            {
                _reported = true;
                Guard.Report(e, "MonsterAI.UpdateAI raider walking off");
            }
        }
    }
}

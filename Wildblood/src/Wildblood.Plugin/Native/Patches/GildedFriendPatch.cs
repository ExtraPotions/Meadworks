// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Marks the game's two friend searches, so a Gilded creature's "players are not my enemies" does not make them its
    /// friends. The game calls anyone who is not an enemy a friend, and a monster with a friend weapon (a Greydwarf
    /// shaman's heal) walks up to a hurt friend to use it - a Gilded shaman would approach a hurt player, then flee, and
    /// again. While a search runs, <see cref="GildedEnemyPatch"/> gives the game's own answer for players, so they stay
    /// what they were. The patch is on the list-taking searches every caller goes through (the monster's attack choice
    /// and its friend weapons); they hold loops, so they are not inlined away. The finalizer clears the mark whatever
    /// happens, so nothing outside a search ever sees it.
    /// </summary>
    [HarmonyPatch]
    public static class GildedFriendPatch
    {
        /// <summary>True while one of the game's friend searches is running.</summary>
        public static bool Active { get; private set; }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] parameters = { typeof(List<Character>), typeof(float) };
            yield return AccessTools.Method(typeof(BaseAI), "HaveFriendInRange", parameters);
            yield return AccessTools.Method(typeof(BaseAI), "HaveHurtFriendInRange", parameters);
        }

        private static void Prefix() => Active = true;

        private static Exception? Finalizer(Exception? __exception)
        {
            Active = false;
            return __exception;
        }
    }
}

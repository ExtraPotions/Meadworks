// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using HarmonyLib;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// A Gilded creature never counts a player as its enemy, so the game itself keeps it from targeting, chasing or
    /// striking one: its target is dropped the step it is set, and its swings and shots pass through players the way
    /// they pass through its own kind. What it does about players instead - run - is <see cref="GildedFleePatch"/>'s
    /// job. Asymmetric like the Devouring override (<see cref="EnemyPatch"/>): only the Gilded creature's own view
    /// changes, so players still fight it and its health bar still reads as an enemy's. An animal is not registered and
    /// keeps players as enemies, because that is what makes it run from them. <see cref="Vanilla"/> asks the game's
    /// own answer for the flee check - a player the game would not call its enemy, a tamed creature's friend, is nobody
    /// to run from - and while it asks, every mod override stands aside (<see cref="GameOnly"/>, which the Devouring
    /// override honours too), so a Gilded devourer that is feeding still sees players as the threat they are. The
    /// game's friend searches (<see cref="GildedFriendPatch"/>) also get the game's own answer: "not an enemy" must not
    /// turn a player into a friend to heal or buff. It runs last among the IsEnemy postfixes, so the answer is its own
    /// whatever else has overridden it. IsEnemy is asked constantly, so the first test is one static check.
    /// </summary>
    [HarmonyPatch(typeof(BaseAI), "IsEnemy", new[] { typeof(Character), typeof(Character) })]
    public static class GildedEnemyPatch
    {
        /// <summary>True while <see cref="Vanilla"/> asks: every mod override of IsEnemy stands aside.</summary>
        public static bool GameOnly { get; private set; }

        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Character a, Character b, ref bool __result)
        {
            if (!GildedBehaviour.Any || GameOnly || b == null || !b.IsPlayer() || !GildedBehaviour.ShunsPlayers(a))
            {
                return;
            }
            __result = GildedFriendPatch.Active && Vanilla(a, b); // never an enemy; never a friend either
        }

        /// <summary>Whether the game itself, mod overrides aside, counts <paramref name="b"/> as <paramref name="a"/>'s
        /// enemy.</summary>
        public static bool Vanilla(Character a, Character b)
        {
            GameOnly = true;
            try
            {
                return BaseAI.IsEnemy(a, b);
            }
            finally
            {
                GameOnly = false;
            }
        }
    }
}

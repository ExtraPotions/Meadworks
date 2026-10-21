// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Runtime;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The echoes loaded on this machine, each with the boss it replays, so the patches that ask of every creature - its
    /// nameplate, who counts it an enemy, where it aims - find an echo in one lookup and cost one count check while none
    /// is loaded. On the echo's owner it also holds where each echo aims right now: the spot its boss's target stood at
    /// when the boss did what the echo now does (<see cref="EchoDriver"/>). An echo joins as it is veiled
    /// (<see cref="EchoBody.Veil"/>) and leaves when it is destroyed.
    /// </summary>
    internal static class EchoLink
    {
        private static readonly Dictionary<Character, ZDOID> Bosses = new Dictionary<Character, ZDOID>();
        private static readonly Dictionary<Character, Vector3> Aims = new Dictionary<Character, Vector3>();

        public static bool Any => Bosses.Count > 0;

        public static bool IsEcho(Character character) => Bosses.Count > 0 && Bosses.ContainsKey(character);

        public static void Join(Character echo, ZDOID boss) => Bosses[echo] = boss;

        // `is null` rather than Unity's ==: the echo may already read as destroyed here, and its entries must go.
        public static void Leave(Character? echo)
        {
            if (echo is null)
            {
                return;
            }
            Bosses.Remove(echo);
            Aims.Remove(echo);
        }

        /// <summary>The loaded echo of <paramref name="boss"/>, or null.</summary>
        public static Character? Find(ZDOID boss)
        {
            foreach (KeyValuePair<Character, ZDOID> pair in Bosses)
            {
                if (pair.Value == boss && pair.Key != null)
                {
                    return pair.Key;
                }
            }
            return null;
        }

        /// <summary>The boss an echo replays, when it is loaded here; null otherwise.</summary>
        public static Character? BossOf(Character echo)
        {
            if (!Bosses.TryGetValue(echo, out ZDOID boss) || ZNetScene.instance == null)
            {
                return null;
            }
            GameObject found = ZNetScene.instance.FindInstance(boss);
            return found != null ? found.GetComponent<Character>() : null;
        }

        /// <summary>
        /// Whose aspects an echo's blow carries: its boss's, when the boss is loaded here and ready, so the echo's blow
        /// lands as hard as the boss's did - its Enraged, a Twin's cut and the rest; the echo itself otherwise.
        /// </summary>
        public static EliteController Source(EliteController attacker)
        {
            if (!attacker.Traits.Echo || attacker.Creature == null)
            {
                return attacker;
            }
            Character? boss = BossOf(attacker.Creature);
            EliteController? controller = boss != null ? boss.GetComponent<EliteController>() : null;
            return controller != null && controller.Ready ? controller : attacker;
        }

        public static void Aim(Character echo, EchoPose pose)
        {
            if (pose.Aimed)
            {
                Aims[echo] = pose.Aim;
            }
            else
            {
                Aims.Remove(echo);
            }
        }

        public static bool TryAim(Character character, out Vector3 point)
        {
            point = default;
            return Aims.Count > 0 && Aims.TryGetValue(character, out point);
        }
    }
}

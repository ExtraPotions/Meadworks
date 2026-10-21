// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One Phantom split, on the boss's owner, each time its health falls past a `split at` mark
    /// (<see cref="PhantomBehaviour"/>): `per player` copies for each player online, so a player alone faces one and a
    /// group of four faces four, and the boss is hidden among them. Boss and copies take the places of one even ring around
    /// the spot the boss stood on, the boss's place drawn at random, behind a puff at every place
    /// (<see cref="PhantomShuffle"/>). Each copy is the boss's double: same prefab, stars, size, aspects and so name, and it
    /// starts at the boss's own share of health left, so no bar gives it away. Each is marked in its own ZDO with the boss
    /// it belongs to. The mark is what makes a copy a copy on every machine (<see cref="PhantomBody"/>), and what lets the
    /// boss's death find and dismiss them (<see cref="PhantomReaper"/>).
    /// </summary>
    internal static class PhantomSpawner
    {
        private const float Radius = 5f;

        /// <summary>A sanity cap on one split, however many players are online or whatever the rule file says.</summary>
        private const int MaxCopies = 16;

        public static void Spawn(EliteController boss)
        {
            int players = PlayersOnline();
            int copies = Mathf.Clamp(
                Mathf.RoundToInt(players * AspectMath.Power(Aspect.Phantom, Fields.PerPlayer)), 0, MaxCopies);
            if (copies == 0)
            {
                return;
            }
            Vector3 centre = boss.transform.position;
            List<Vector3> places = Ring(centre, copies + 1);
            int slot = Random.Range(0, places.Count);
            Vector3 dest = places[slot];
            places.RemoveAt(slot);
            AspectStore.SetPhantomOrder(boss.View.GetZDO(), Random.value);
            PhantomShuffle.Send(boss, dest, places); // drawn here at once, while the boss still stands where it vanishes
            Split(boss, places);
            PhantomShuffle.Move(boss, dest, centre);
            if (Log.Diagnostics)
            {
                Log.Diag($"{boss.name}: split off {copies} phantom copies for {players} players online and hid among them");
            }
        }

        /// <summary><paramref name="count"/> places evenly round the centre, from a random start.</summary>
        private static List<Vector3> Ring(Vector3 centre, int count)
        {
            float start = Random.Range(0f, 360f);
            List<Vector3> ring = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                ring.Add(SpawnPlace.Around(centre, start + 360f * i / count, Radius));
            }
            return ring;
        }

        /// <summary>
        /// The copies, one per place. A copy wears the boss's aspects exactly, headline and extras, so its name is the
        /// boss's; what makes it a copy is its mark, which keeps Phantom's own splits, drops and death off it
        /// (<see cref="Runtime.AspectInstaller"/>), and a Bountiful boss's copies fight with its other aspects as before.
        /// </summary>
        private static void Split(EliteController boss, List<Vector3> places)
        {
            ZDOID bossId = boss.View.GetZDO().m_uid;
            float share = Mathf.Clamp01(boss.Creature.GetHealthPercentage());
            CreatureTraits traits = new CreatureTraits(boss.Traits.Stars, boss.Traits.Aspect) { ExtraAspects = boss.Traits.ExtraAspects };
            foreach (Vector3 place in places)
            {
                Rouse(BossCopy.Make(boss, place, traits, copy => Mark(copy, bossId, share)));
            }
        }

        /// <summary>The copy's mark, its place in the bar row, and its health at the boss's share of its own maximum.</summary>
        private static void Mark(ZDO copy, ZDOID boss, float share)
        {
            AspectStore.SetPhantomOf(copy, boss);
            AspectStore.SetPhantomOrder(copy, Random.value);
            copy.Set(ZDOVars.s_health, AspectMath.PhantomHealth() * share);
        }

        /// <summary>
        /// A copy splits off mid-fight, so it is born awake: a boss prefab that starts asleep (the Elder rising from the
        /// ground) would otherwise lie down and play its whole stand-up before fighting. Cleared in the frame it is made,
        /// before its animator first runs and before its ZDO first leaves this machine, so no machine ever sees it asleep;
        /// no wake-up effect plays.
        /// </summary>
        internal static void Rouse(Character? copy)
        {
            MonsterAI ai = copy != null ? copy.GetComponent<MonsterAI>() : null!;
            if (ai == null || !ai.IsSleeping() || ai.m_nview == null || !ai.m_nview.IsValid())
            {
                return;
            }
            ai.m_sleeping = false;
            ai.m_nview.GetZDO().Set(ZDOVars.s_sleeping, false);
            ai.m_animator.SetBool(ZSyncAnimation.GetHash("sleeping"), false);
        }

        /// <summary>
        /// Everyone on the server, whichever machine owns the boss: the server keeps the list and sends it to every
        /// client, and it never counts a dedicated server itself. Never less than one.
        /// </summary>
        private static int PlayersOnline() => ZNet.instance != null ? Mathf.Max(1, ZNet.instance.GetNrOfPlayers()) : 1;
    }
}

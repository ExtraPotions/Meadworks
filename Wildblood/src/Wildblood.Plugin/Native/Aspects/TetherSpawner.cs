// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Tethered's arrival: on the boss's owner, the moment the boss is first rolled, a second boss of the same kind
    /// appears beside it with the same stars and every aspect the first carries, and each is written into the other's ZDO
    /// as its partner (<see cref="TetherPair"/>). Unlike a twin the two keep their own health and die apart; the link is
    /// only what the tether measures and is drawn between. The second is born already linked and resolved, so it never
    /// reaches this and never brings a partner of its own.
    /// </summary>
    internal static class TetherSpawner
    {
        /// <summary>Metres from the boss the second appears: a little wider than a twin, so the tether shows from the start.</summary>
        private const float Distance = 6f;

        public static void Spawn(EliteController boss)
        {
            ZDO zdo = boss.View.GetZDO();
            if (AspectStore.GetTether(zdo) != ZDOID.None)
            {
                return; // already a pair
            }
            ZDOID bossId = zdo.m_uid;
            Vector3 pos = SpawnPlace.Around(boss.transform.position, Random.Range(0f, 360f), Distance);
            CreatureTraits traits = new CreatureTraits(boss.Traits.Stars, boss.Traits.Aspect)
            {
                ExtraAspects = boss.Traits.ExtraAspects,
            };
            Character? partner = BossCopy.Make(boss, pos, traits, copy => AspectStore.SetTether(copy, bossId));
            if (partner != null)
            {
                AspectStore.SetTether(zdo, partner.GetZDOID());
                if (Log.Diagnostics)
                {
                    Log.Diag($"{boss.name}: tethered partner spawned, {traits.Stars} star(s)");
                }
            }
        }
    }
}

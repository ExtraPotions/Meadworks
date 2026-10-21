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
    /// Twin's arrival: on the boss's owner, the moment the boss is first rolled, a second copy of it appears beside it
    /// with the same stars and every aspect it carries - a Bountiful boss's twin is Bountiful with the same extras, so the
    /// two are the same fight and each pays the same loot - and each is written into the other's ZDO as its partner. The
    /// link is what <see cref="TwinLink"/> shares health through, and what stops the twin - born already linked - bringing
    /// a twin of its own.
    /// </summary>
    internal static class TwinSpawner
    {
        private const float Distance = 4f;

        public static void Spawn(EliteController boss)
        {
            ZDO zdo = boss.View.GetZDO();
            if (AspectStore.GetTwin(zdo) != ZDOID.None)
            {
                return; // already a pair
            }
            ZDOID bossId = zdo.m_uid;
            Vector3 pos = SpawnPlace.Around(boss.transform.position, Random.Range(0f, 360f), Distance);
            CreatureTraits traits = new CreatureTraits(boss.Traits.Stars, boss.Traits.Aspect)
            {
                ExtraAspects = boss.Traits.ExtraAspects,
            };
            Character? twin = BossCopy.Make(boss, pos, traits, copy => AspectStore.SetTwin(copy, bossId));
            if (twin != null)
            {
                AspectStore.SetTwin(zdo, twin.GetZDOID());
                if (Log.Diagnostics)
                {
                    Log.Diag($"{boss.name}: twin spawned, {traits.Stars} star(s)");
                }
            }
        }
    }
}

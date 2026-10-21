// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
// Boss progression and material behavior adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
using System;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Makes another of a boss's own prefab for Twin, Tethered and Phantom, on the boss's owner. The copy is born
    /// already resolved - its traits, biome and aspect link are written to its ZDO in the frame it is instantiated, before
    /// its own controller wakes - so its owner never rolls it, it never brings a twin or copies of its own, and every
    /// client reads the same thing. It wakes the way the altar wakes the boss: patrolling where it stands, alerted if the
    /// boss is.
    /// </summary>
    internal static class BossCopy
    {
        public static Character? Make(EliteController boss, Vector3 pos, CreatureTraits traits, Action<ZDO> link)
        {
            ZDO bossZdo = boss.View.GetZDO();
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(bossZdo.GetPrefab()) : null;
            if (prefab == null)
            {
                Log.Warn($"{boss.name}: no prefab to make its copy from ({string.Join(" ", traits.Aspects())})");
                return null;
            }
            GameObject copy = Object.Instantiate(prefab, pos, boss.transform.rotation);
            ZNetView nview = copy.GetComponent<ZNetView>();
            Character character = copy.GetComponent<Character>();
            if (nview == null || character == null || nview.GetZDO() == null)
            {
                return null;
            }
            Mark(nview.GetZDO(), bossZdo, traits, link);
            character.SetLevel(boss.Creature.GetLevel()); // 1 for a starred boss; a kept level is the copy's too
            Wake(boss, copy);
            return character;
        }

        private static void Mark(ZDO zdo, ZDO bossZdo, CreatureTraits traits, Action<ZDO> link)
        {
            link(zdo);
            if(traits.HasAspect(Aspect.Twin)||traits.HasAspect(Aspect.Tethered))BossProgress.Inherit(bossZdo,zdo);
            TraitStore.Save(zdo, traits);
            TraitStore.SetBiome(zdo, TraitStore.GetBiome(bossZdo));
        }

        private static void Wake(EliteController boss, GameObject copy)
        {
            BaseAI ai = copy.GetComponent<BaseAI>();
            if (ai == null)
            {
                return;
            }
            ai.SetPatrolPoint();
            BaseAI bossAi = boss.GetComponent<BaseAI>();
            if (bossAi != null && bossAi.IsAlerted())
            {
                ai.Alert();
            }
        }
    }
}

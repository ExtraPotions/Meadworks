// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.21.0, GPL-3.0-or-later
// 9da28eb58adbe065be1e4baef779a448acd38e0f
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Mutations;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Makes an Echoing boss's echo, on the boss's owner, where and as the boss stood `delay` seconds ago: another of its
    /// own prefab, born already resolved with the boss's stars and no aspect (so it is never rolled, and none of the
    /// boss's aspects ever acts through it), the boss's biome, level and gear seed (so it holds the same weapons), and
    /// marked as the boss's echo in its ZDO in the frame it is made. It is awake (a boss that starts asleep, the Elder,
    /// would otherwise lie down first) and veiled at once (<see cref="EchoBody.Veil"/>), before it is ever drawn. It is
    /// not persistent: never saved, and gone with its owner or its zone, so a reload never finds one; the boss's next
    /// owner makes a new one once it has recorded the delay again.
    /// </summary>
    internal static class EchoSpawner
    {
        private static bool _warned;

        public static Character? Make(EliteController boss, EchoPose pose)
        {
            ZDO bossZdo = boss.View.GetZDO();
            GameObject? prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(bossZdo.GetPrefab()) : null;
            if (prefab == null)
            {
                Warn(boss);
                return null;
            }
            GameObject copy = Object.Instantiate(prefab, pose.Position, pose.Rotation);
            Character echo = copy.GetComponent<Character>();
            ZNetView view = copy.GetComponent<ZNetView>();
            if (echo == null || view == null || view.GetZDO() == null)
            {
                Object.Destroy(copy); // never left behind as an unmarked, full-strength second boss
                return null;
            }
            Mark(view.GetZDO(), bossZdo, boss);
            echo.SetLevel(boss.Creature.GetLevel());
            CloneSpawner.Arm(echo, boss.Creature, view.GetZDO());
            PhantomSpawner.Rouse(echo);
            EchoBody.Veil(echo, bossZdo.m_uid);
            if (Log.Diagnostics)
            {
                Log.Diag($"{boss.name}: its echo rose, {EchoTape.Delay():0.#} s behind it");
            }
            return echo;
        }

        private static void Mark(ZDO echo, ZDO bossZdo, EliteController boss)
        {
            echo.Persistent = false;
            AspectStore.SetEchoOf(echo, bossZdo.m_uid);
            TraitStore.Save(echo, new CreatureTraits(boss.Traits.Stars, Aspect.None) { Tier = boss.Traits.Tier });
            TraitStore.SetBiome(echo, TraitStore.GetBiome(bossZdo));
        }

        private static void Warn(EliteController boss)
        {
            if (!_warned)
            {
                _warned = true;
                Log.Warn($"{boss.name}: no prefab to make its echo from; it fights without one");
            }
        }
    }
}

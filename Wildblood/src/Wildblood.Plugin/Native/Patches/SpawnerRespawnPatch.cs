// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using HarmonyLib;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Patches
{
    /// <summary>
    /// Makes cleared camps and dungeons repopulate, by giving a one-shot spawner the respawn timer it was built
    /// without. The game's own spawner already knows how to respawn - it keeps the time of its last live creature in
    /// its ZDO and waits <c>m_respawnTimeMinuts</c> minutes of world clock - but dungeon and camp spawners ship with that
    /// set to zero, which is what makes a cleared landmark stay dead. Writing a positive timer hands the work back to
    /// the game, so nothing here has to track a creature or spawn one.
    ///
    /// Only a spawner that has spawned before gets a timer. One that never has keeps zero, so the first population of a
    /// place is the game's own: a timer on a fresh spawner counts from the world's year zero (its last-alive time is
    /// unset), so it would hold the first spawn back until the world itself was older than the timer.
    ///
    /// A spawner whose timer the game already set is never touched: that one respawns as its designer intended.
    /// Runs on every machine; the value is a local field the owner reads, so no state is written and there is nothing
    /// to replicate.
    /// </summary>
    internal static class SpawnerRespawn
    {
        internal static void Arm(CreatureSpawner spawner)
        {
            RespawnRules rules = RuleState.Active.Respawn;
            if (!rules.Camps && !rules.Dungeons)
            {
                return; // both off, the default: every spawner is left as the game made it
            }
            if (spawner == null || spawner.m_respawnTimeMinuts > 0f || !spawner.HasSpawned())
            {
                return;
            }
            float minutes = rules.MinutesFor(InDungeon(spawner));
            if (minutes > 0f)
            {
                spawner.m_respawnTimeMinuts = minutes;
            }
        }

        /// <summary>
        /// A dungeon spawner sits in an interior, which the game builds high above its entrance; a camp's sits on the
        /// ground, Fuling and Draugr villages included. Asked of the position with the game's own interior test,
        /// because no live spawner or chest has its room as a parent: the game creates every networked object of a
        /// room or a location on its own, at the top of the scene.
        /// </summary>
        internal static bool InDungeon(Component spawner) => Character.InInterior(spawner.transform.position);
    }

    /// <summary>A spawner loading with a creature already behind it (alive or killed) is armed before its first update.</summary>
    [HarmonyPatch(typeof(CreatureSpawner), "Awake")]
    public static class SpawnerRespawnPatch
    {
        private static void Postfix(CreatureSpawner __instance) =>
            Guard.Run("CreatureSpawner.Awake respawn", static spawner => SpawnerRespawn.Arm(spawner), __instance);
    }

    /// <summary>A spawner that has just spawned its first creature is armed for the next one.</summary>
    [HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
    public static class SpawnerFirstSpawnPatch
    {
        private static void Postfix(CreatureSpawner __instance) =>
            Guard.Run("CreatureSpawner.Spawn respawn", static spawner => SpawnerRespawn.Arm(spawner), __instance);
    }
}

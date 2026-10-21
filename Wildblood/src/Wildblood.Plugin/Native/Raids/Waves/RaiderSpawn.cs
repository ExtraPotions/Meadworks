// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// Changed by expDARE, 2026: Wildblood namespace and authority-owned raid integration.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Wildblood.Adopted.Raids
{
    /// <summary>
    /// One raider arriving, on the host's owner, which then owns it: the game's own Instantiate of the creature's prefab
    /// (as the Summoner's adds arrive, Aspects/SummonWave.cs) a few metres around the wave's spot, facing the host. In the
    /// same frame, before its controller's first roll: the level the game rolls for an event spawn (it stands where this
    /// mod's stars are off; where they are on the controller sets it back to 1 and the stars rule), its stars and
    /// mutations (<see cref="RaiderTraits.Roll"/>) and an alert, so it arrives looking for a fight. It is not made a game
    /// event creature: that flag sends a creature away whenever no game event runs here, and when a raider leaves is the
    /// raid's to say. Nor is it set to hunt players across the map: it fights the players it meets and goes for the base
    /// (<see cref="RaiderSteering"/>). The game plays no effect for an event spawn, so neither does this. The caller tags
    /// it.
    /// </summary>
    internal static class RaiderSpawn
    {
        private static readonly HashSet<string> Warned = new HashSet<string>();

        /// <summary>The raider's ZDO, or null when the creature could not be made (logged once per prefab).</summary>
        public static ZDO? Arrive(RaidRunner runner, SpawnSystem.SpawnData data, Vector3 spot, bool warlord)
        {
            try
            {
                return Make(runner, data, spot, warlord);
            }
            catch (Exception e)
            {
                string prefab = data.m_prefab != null ? data.m_prefab.name : "?";
                if (Warned.Add(prefab))
                {
                    Guard.Report(e, "raider " + prefab);
                }
                return null;
            }
        }

        private static ZDO? Make(RaidRunner runner, SpawnSystem.SpawnData data, Vector3 spot, bool warlord)
        {
            Vector3 pos = WaveRing.Around(spot, data.m_groundOffset);
            GameObject made = Object.Instantiate(data.m_prefab, pos, Facing(runner.Position - pos));
            Character? creature = made.GetComponent<Character>();
            ZNetView? view = made.GetComponent<ZNetView>();
            if (creature == null || view == null || view.GetZDO() == null)
            {
                Object.Destroy(made);
                return null;
            }
            try
            {
                Ready(runner, creature, data, pos, warlord);
            }
            catch
            {
                ZNetScene.instance.Destroy(made); // half made: gone, rather than left by the base untagged
                throw;
            }
            return view.GetZDO();
        }

        private static void Ready(RaidRunner runner, Character creature, SpawnSystem.SpawnData data, Vector3 pos, bool warlord)
        {
            GameLevel(creature, data, pos);
            RaiderTraits.Roll(creature, runner.Biome, runner.Band, warlord);
            BaseAI? ai = creature.GetComponent<BaseAI>();
            if (ai != null)
            {
                ai.Alert();
            }
        }

        // The game's own event roll (SpawnSystem.Spawn): from the entry's lowest level, one more each time the level-up
        // chance comes up, to its highest - where the entry allows levels this far from the world's centre.
        private static void GameLevel(Character creature, SpawnSystem.SpawnData data, Vector3 pos)
        {
            if (data.m_levelUpMinCenterDistance > 0f && pos.magnitude <= data.m_levelUpMinCenterDistance)
            {
                return;
            }
            int level = data.m_minLevel;
            float chance = SpawnSystem.GetLevelUpChance(pos, data);
            while (level < data.m_maxLevel && Dice.Draw() <= chance)
            {
                level++;
            }
            if (level > 1)
            {
                creature.SetLevel(level);
            }
        }

        private static Quaternion Facing(Vector3 toHost)
        {
            toHost.y = 0f;
            return toHost.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toHost) : Quaternion.identity;
        }
    }
}

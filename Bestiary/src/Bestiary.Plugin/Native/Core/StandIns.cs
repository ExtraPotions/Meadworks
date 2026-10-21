// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.9.0 at ValheimMods 3bb778330f23645ccddb42fbc0c6ffab72c842b6
// (Core/StandIns.cs, Core/StandInPatches.cs).
// Changed by expDARE, 2026: Bestiary namespace; the counting rules live in Bestiary.Core's StandInTable.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Spawning;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Core
{
    /// <summary>
    /// Bestiary's creatures that spawn in place of a game creature, counted as that creature by the game's spawners
    /// (<see cref="StandInSpawnAreaPatch"/>, <see cref="StandInWildCountPatch"/>): uncounted, a bone pile or greydwarf
    /// nest keeps spawning until the room is full, and a wild spawn passes its cap. Registered once each when built,
    /// identically on every peer.
    /// </summary>
    public static class StandIns
    {
        internal static readonly StandInTable Table = new StandInTable();

        public static void Add(GameObject? standIn, params string[] games)
        {
            if (standIn != null)
                Table.Add(standIn.name.GetStableHashCode(), games.Select(g => g.GetStableHashCode()).ToArray());
        }

        public static bool CountsFor(SpawnArea area, GameObject creature)
        {
            ZNetView view = creature.GetComponent<ZNetView>();
            ZDO? zdo = view != null ? view.GetZDO() : null;
            if (zdo == null)
                return false;
            Character character = creature.GetComponent<Character>();
            return Table.CountsFor(zdo.GetPrefab(), character != null && character.IsTamed(),
                area.m_prefabs.Where(d => d.m_prefab != null).Select(d => d.m_prefab.name.GetStableHashCode()));
        }

        public static int Count(GameObject prefab, List<ZDO> zdos, bool eventCreaturesOnly) =>
            Table.IsEmpty ? 0 : Table.Count(prefab.name.GetStableHashCode(), zdos.Select(z => (z.GetPrefab(), z.GetBool(ZDOVars.s_eventCreature))), eventCreaturesOnly);
    }

    /// <summary>A spawn area (bone pile, greydwarf nest) counts untamed stand-ins as its own creatures, so its near and total caps hold.</summary>
    [HarmonyPatch(typeof(SpawnArea), nameof(SpawnArea.IsSpawnPrefab))]
    public static class StandInSpawnAreaPatch
    {
        private static void Postfix(SpawnArea __instance, GameObject go, ref bool __result)
        {
            if (!__result && go != null)
                __result = SafeCall.Run("SpawnArea.IsSpawnPrefab stand-ins", static (area, creature) => StandIns.CountsFor(area, creature), __instance, go, false);
        }
    }

    /// <summary>A wild spawn's cap counts stand-ins as the creature they replaced.</summary>
    [HarmonyPatch(typeof(SpawnSystem), nameof(SpawnSystem.GetNrOfZDOInstances))]
    public static class StandInWildCountPatch
    {
        private static void Postfix(GameObject prefab, List<ZDO> ZDOs, bool eventCreaturesOnly, ref int __result)
        {
            if (prefab != null && ZDOs != null)
                __result += SafeCall.Run("SpawnSystem.GetNrOfZDOInstances stand-ins", static (game, zdos, events) => StandIns.Count(game, zdos, events), prefab, ZDOs, eventCreaturesOnly, 0);
        }
    }
}

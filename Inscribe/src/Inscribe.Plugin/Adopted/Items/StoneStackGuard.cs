// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Items
{
    /// <summary>
    /// Keeps a loaded stone stack whole when it is larger than the stone's current max stack (Phase 1 decision IMP-7).
    /// <c>Inventory.Load</c> ends in the private 14-argument <c>Inventory.AddItem</c>, which clamps every stack to
    /// <c>m_maxStackSize</c> (decompile: <c>Mathf.Min(stack, m_maxStackSize)</c>). A server may raise a stone's
    /// <c>stack</c>; a joining player's inventory (or a container, or a tombstone) can load before the server's economy
    /// article is adopted, while the prefab still holds this machine's own value, and the surplus would be lost.
    /// While one stone stack loads, its prefab's max stack is raised to the stack being loaded and restored after, so the
    /// stack loads whole; once the server's rules arrive the prefab takes the server's value. A stack that is still larger
    /// than the configured max (a server that lowered it) stays oversized rather than losing stones.
    /// Runs wherever an inventory loads: every client for its player, containers and tombstones on their ZDO owner.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), "AddItem", new[]
    {
        typeof(int), typeof(int), typeof(float), typeof(Vector2i), typeof(bool), typeof(int), typeof(int), typeof(long),
        typeof(string), typeof(Dictionary<string, string>), typeof(int), typeof(bool), typeof(bool), typeof(bool),
    })]
    internal static class StoneStackGuard
    {
        // __state: the max stack to restore, 0 when nothing was raised.
        private static void Prefix(int prefabHash, int stack, out int __state)
        {
            __state = 0;
            if (stack <= 1 || ObjectDB.instance == null)
            {
                return;
            }
            StoneEntry? entry = StonePrefabs.EntryOf(ObjectDB.instance.GetItemPrefab(prefabHash));
            if (entry == null || stack <= entry.Shared.m_maxStackSize)
            {
                return;
            }
            __state = entry.Shared.m_maxStackSize;
            entry.Shared.m_maxStackSize = stack;
        }

        // A finalizer, so the prefab's value is restored even when the game's method throws (void: the exception stays).
        private static void Finalizer(int prefabHash, int __state)
        {
            if (__state <= 0 || ObjectDB.instance == null)
            {
                return;
            }
            StoneEntry? entry = StonePrefabs.EntryOf(ObjectDB.instance.GetItemPrefab(prefabHash));
            if (entry != null)
            {
                entry.Shared.m_maxStackSize = __state;
            }
        }
    }
}

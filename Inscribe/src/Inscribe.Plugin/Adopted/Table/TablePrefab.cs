// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the table's
// bundle model, shelf, bowl, gems and cast effects are not adopted: the table wears the workbench and its icon.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Bundles;
using ExtraPotions.Inscribe.Adopted.Core;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// The Rune Table piece <c>ECF_RuneTable</c> (rune-table.md section 2): a copy of the game's workbench (its size,
    /// placement, wear, sounds, wood and look) without the crafting station, with <see cref="RuneTable"/> added. Made once per process on every peer,
    /// dedicated server included, under PrefabBench's inactive bench (no Awake, no ZDO), and added to the scene's
    /// prefab list before ZNetScene.Awake registers it, whether the table is switched on or not, so tables already
    /// built always load. The hammer and the cost are <see cref="TableHammer"/>'s.
    /// </summary>
    [HarmonyPatch]
    internal static class TablePrefab
    {
        public const string PrefabName = "ECF_RuneTable";
        private const string Source = "piece_workbench";

        /// <summary>The table prefab; null until the scene first woke (or when the game's workbench is missing).</summary>
        public static GameObject? Prefab { get; private set; }

        public static Piece? Piece => Prefab != null ? Prefab.GetComponent<Piece>() : null;

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        [HarmonyPrefix]
        private static void SceneAwake(ZNetScene __instance)
        {
            try
            {
                AddTo(__instance.m_prefabs);
            }
            catch (Exception e)
            {
                // Never out of ZNetScene.Awake: a throw there leaves the world loading for ever.
                Log.Error($"the Rune Table could not be made: {e}");
            }
        }

        private static void AddTo(List<GameObject> prefabs)
        {
            if (Prefab == null)
            {
                Prefab = Build(prefabs.Find(p => p != null && p.name == Source));
            }
            if (Prefab != null && !prefabs.Contains(Prefab))
            {
                prefabs.Add(Prefab);
            }
        }

        private static GameObject? Build(GameObject? workbench)
        {
            if (workbench == null || workbench.GetComponent<Piece>() == null)
            {
                Log.Warn($"the game's {Source} was not found: there is no Rune Table");
                return null;
            }
            GameObject copy = PrefabBench.Copy(workbench, PrefabName);
            Object.DestroyImmediate(copy.GetComponent<CraftingStation>());
            foreach (CircleProjector marker in copy.GetComponentsInChildren<CircleProjector>(true))
            {
                Object.DestroyImmediate(marker.gameObject);
            }
            Configure(copy.GetComponent<Piece>(), workbench);
            copy.AddComponent<RuneTable>();
            return copy;
        }

        private static void Configure(Piece piece, GameObject workbench)
        {
            piece.m_name = TableWords.Name;
            piece.m_description = TableWords.Description;
            piece.m_category = Piece.PieceCategory.Crafting;
            piece.m_enabled = true;
            piece.m_craftingStation = workbench.GetComponent<CraftingStation>();
        }
    }
}

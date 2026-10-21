// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGatePiece.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The pillar prefab <see cref="SeaGateFields.PillarPrefab"/>: a runtime copy of one of the game's own tall
    /// pieces, stretched to about 4.5 m so the surface between two pillars reads well, with <see cref="SeaGatePillar"/>
    /// added. Made once per process on every machine, dedicated server included, and whether sea gates are on or not,
    /// so pillars already built always load. The copy is made under an inactive holder kept with DontDestroyOnLoad, so
    /// none of its Awakes run and it never gets a ZDO.
    /// <list type="bullet">
    /// <item>After ZNetScene.Awake (each world load) it is added to the scene's prefab list and to its name table under
    /// <see cref="SeaGateFields.PillarHash"/>, before any ZDO is turned into an object.</item>
    /// <item>Once both the scene and the item database exist (whichever wakes second), the recipe is applied
    /// (<see cref="SeaGatePieceRecipe"/>) and the piece added to the Hammer's build table, Misc tab, at a workbench.</item>
    /// </list>
    /// Everything is idempotent: the item database also wakes in the start scene, where there is no net scene.</summary>
    public static class SeaGatePiece
    {
        public const string WorkbenchPrefab = "piece_workbench";
        public const string HammerItem = "Hammer";

        /// <summary>A game piece the pillar may be copied from, and the scale that makes it about 4.5 m tall. Tried in
        /// order; the first the net scene has wins. Heights of the game's prefabs (stone_pillar 2 m, wood_pole_log_4 4.4 m,
        /// wood_pole2 2 m tall).</summary>
        private readonly struct Source
        {
            public readonly string Name;
            public readonly Vector3 Scale;

            public Source(string name, Vector3 scale)
            {
                Name = name;
                Scale = scale;
            }
        }

        private static readonly Source[] Sources =
        {
            new Source("stone_pillar", new Vector3(1.25f, 2.25f, 1.25f)),
            new Source("wood_pole_log_4", new Vector3(1.5f, 1f, 1.5f)),
            new Source("wood_pole2", new Vector3(1.5f, 2.25f, 1.5f))
        };

        private static GameObject holder;
        private static bool missingLogged;

        /// <summary>The pillar prefab; null until the net scene first woke (or when none of the sources exists).</summary>
        public static GameObject Prefab { get; private set; }

        /// <summary>The prefab's Piece, the one the hammer and the build menu hand around.</summary>
        public static Piece Piece { get; private set; }

        /// <summary>Whether a placement ghost or object is a sea gate pillar.</summary>
        public static bool IsPillar(GameObject go) => go != null && go.GetComponent<SeaGatePillar>() != null;

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
        internal static class SceneAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => Run(Install);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        internal static class DatabaseAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => Run(Install);
        }

        public static void Install()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
                return;
            if (Prefab == null)
                Build(scene);
            if (Prefab == null)
                return;
            Register(scene);
            if (ObjectDB.instance == null)
                return;
            SeaGatePieceRecipe.Apply();
            AddToHammer(ObjectDB.instance);
        }

        private static void Build(ZNetScene scene)
        {
            GameObject source = FindSource(scene, out Vector3 scale);
            if (source == null)
            {
                if (!missingLogged)
                    SeaGateLog.LogWarning("None of the game's pillar pieces was found: there is no Sea Gate Pillar.");
                missingLogged = true;
                return;
            }
            GameObject copy = Object.Instantiate(source, Holder.transform, false);
            copy.name = SeaGateFields.PillarPrefab;
            copy.transform.localScale = Vector3.Scale(copy.transform.localScale, scale);
            StripPortal(copy);
            Configure(copy.GetComponent<Piece>(), scene);
            copy.AddComponent<SeaGatePillar>();
            Piece = copy.GetComponent<Piece>();
            Prefab = copy;
            SeaGateLog.LogInfo($"Sea Gate Pillar made from the game's {source.name}.");
        }

        /// <summary>The first source piece the net scene has, with a Piece and a ZNetView.</summary>
        private static GameObject FindSource(ZNetScene scene, out Vector3 scale)
        {
            foreach (Source source in Sources)
            {
                GameObject prefab = scene.GetPrefab(source.Name);
                if (prefab != null && prefab.GetComponent<Piece>() != null && prefab.GetComponent<ZNetView>() != null)
                {
                    scale = source.Scale;
                    return prefab;
                }
            }
            scale = Vector3.one;
            return null;
        }

        /// <summary>A pillar must never be a walk-in portal: <c>PortalDiscovery</c> treats anything with a
        /// <see cref="TeleportWorld"/> as one. None of the sources has it; this keeps it that way.</summary>
        private static void StripPortal(GameObject copy)
        {
            foreach (TeleportWorld portal in copy.GetComponentsInChildren<TeleportWorld>(true))
                Object.DestroyImmediate(portal);
        }

        private static void Configure(Piece piece, ZNetScene scene)
        {
            piece.m_name = SeaGateWords.PillarName;
            piece.m_description = SeaGateWords.PillarDescription;
            piece.m_category = Piece.PieceCategory.Misc;
            piece.m_enabled = true;
            piece.m_waterPiece = false;
            piece.m_noInWater = false;
            piece.m_craftingStation = Workbench(scene);
            piece.m_resources = Array.Empty<Piece.Requirement>();
        }

        private static CraftingStation Workbench(ZNetScene scene)
        {
            GameObject workbench = scene.GetPrefab(WorkbenchPrefab);
            return workbench != null ? workbench.GetComponent<CraftingStation>() : null;
        }

        private static void Register(ZNetScene scene)
        {
            if (!scene.m_prefabs.Contains(Prefab))
                scene.m_prefabs.Add(Prefab);
            scene.m_namedPrefabs[SeaGateFields.PillarHash] = Prefab;
        }

        private static void AddToHammer(ObjectDB database)
        {
            GameObject hammer = database.GetItemPrefab(HammerItem);
            ItemDrop item = hammer != null ? hammer.GetComponent<ItemDrop>() : null;
            PieceTable table = item != null ? item.m_itemData.m_shared.m_buildPieces : null;
            if (table != null && !table.m_pieces.Contains(Prefab))
                table.m_pieces.Add(Prefab);
        }

        private static GameObject Holder
        {
            get
            {
                if (holder != null)
                    return holder;
                holder = new GameObject("Threshold_Prefabs");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
                return holder;
            }
        }

        /// <summary>Runs an Awake hook without ever letting an exception out: a throw from a ZNetScene.Awake patch
        /// disables the net scene and the world never finishes loading.</summary>
        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                SeaGateLog.LogError($"Sea Gate Pillar setup failed: {e}");
            }
        }
    }
}

// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original implementation of the approved floor-roof behavior using own-game API members only.
// No Carturs_Flooring source copied. Original capture/restore follows the existing product's bounded prefab pattern.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Plugin = ExtraPotions.Timber.Runtime.Plugin;

namespace Timber.Flooring
{
    public static class FloorRoofTags
    {
        static readonly Dictionary<GameObject, string> originals = new Dictionary<GameObject, string>();

        public static bool IsFloor(Piece piece) => (piece.m_usage & Piece.UsageTagFlags.Floor) != 0;

        public static void ApplyAll()
        {
            if (!FiresRuntime.RoofsActive) { Reset(); return; }
            if (ZNetScene.instance == null) return;
            // Prefabs first: their original tags provide the baseline for clones made while Timber is active.
            foreach (var prefab in ZNetScene.instance.m_namedPrefabs.Values)
            {
                var piece = prefab != null ? prefab.GetComponent<Piece>() : null;
                if (piece != null) Apply(piece, true);
            }
            foreach (var piece in Piece.s_allPieces)
                if (piece != null) Apply(piece, false);
        }

        public static void Apply(Piece piece, bool prefab)
        {
            if (!FiresRuntime.RoofsActive || !IsFloor(piece)) return;
            Piece? source = prefab ? null : FirePrefabs.Exact(Utils.GetPrefabName(piece.gameObject));
            foreach (var collider in piece.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || collider.GetComponentInParent<Piece>() != piece) continue;
                var target = collider.gameObject;
                if (!originals.ContainsKey(target))
                {
                    string original = target.tag;
                    if (original == "Untagged" && source != null && source != piece)
                    {
                        var matching = MatchingChild(piece.transform, target.transform, source.transform);
                        if (matching != null && originals.TryGetValue(matching.gameObject, out var baseline)) original = baseline;
                    }
                    if (original != "leaky") continue;
                    originals.Add(target, original);
                }
                // Other tags set by external mods are not replaced.
                if (target.CompareTag("leaky")) target.tag = "Untagged";
            }
        }

        // Instantiate preserves sibling paths, including duplicate child names; no string/path registry is needed.
        static Transform? MatchingChild(Transform root, Transform child, Transform template)
        {
            var branch = new Stack<Transform>();
            for (var at = child; at != root; at = at.parent)
            {
                if (at == null) return null;
                branch.Push(at);
            }
            foreach (var expected in branch)
            {
                int index = expected.GetSiblingIndex();
                if (index >= template.childCount) return null;
                template = template.GetChild(index);
                if (template.name != expected.name) return null;
            }
            return template;
        }

        public static void Reset()
        {
            if (originals.Count == 0) return;
            var changed = new HashSet<GameObject>();
            foreach (var entry in originals)
            {
                if (entry.Key == null || !entry.Key.CompareTag("Untagged")) continue;
                entry.Key.tag = entry.Value;
                changed.Add(entry.Key);
            }
            // Native HaveRoof caches a GameObject. Forget only roofs whose tags were restored, and let the game's
            // existing staggered cover checks recompute them; do not force a scene-wide burst of physics casts.
            foreach (var wear in WearNTear.GetAllInstances())
                if (wear != null && wear.m_roof != null && changed.Contains(wear.m_roof)) wear.m_roof = null;
            originals.Clear();
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.Awake))]
        static class Created
        {
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            static void Postfix(Piece __instance)
            {
                if (Plugin.Ready) Apply(__instance, false);
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.OnDestroy))]
        static class Destroyed
        {
            [HarmonyPrefix]
            static void Prefix(Piece __instance)
            {
                foreach (var collider in __instance.GetComponentsInChildren<Collider>(true))
                    if (collider != null) originals.Remove(collider.gameObject);
            }
        }
    }
}

// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Local mesh copies implement the approved food display; no FeedingTrough art or implementation used.
using System;
using System.Collections.Generic;
using ExtraPotions.Shepherd.Runtime.Adopted.Husbandry;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Feeding
{
    internal sealed class TroughFoodVisuals : MonoBehaviour
    {
        static readonly HashSet<TroughFoodVisuals> instances = new HashSet<TroughFoodVisuals>();
        Container? source;
        GameObject? models;
        Bounds surface;
        bool measured, warned;
        float next;
        string shown = "";

        void Awake() { source = GetComponent<Container>(); instances.Add(this); }
        void Update()
        {
            if (!Plugin.AllowsUi || !NativeFeeding.FeedingActive || !ShepherdSettings.ShowFood.Value
                || source == null || !FeedSources.IsUsable(source) || !TroughPieces.IsTrough(source))
            { Hide(); return; }
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1;
            try { Refresh(); }
            catch (Exception error) { Hide(); if (!warned) NativeFeeding.Warn("trough food visual: " + error.GetType().Name + ": " + error.Message); warned = true; }
        }
        void Refresh()
        {
            var food = new List<GameObject>();
            foreach (var item in source!.GetInventory().GetAllItems())
            {
                if (item.m_stack <= 0 || item.m_dropPrefab == null || !FeedSources.Edible().Contains(item.m_shared.m_name)) continue;
                food.Add(item.m_dropPrefab); if (food.Count == 3) break;
            }
            string fingerprint = string.Join(",", food.ConvertAll(item => item.GetInstanceID().ToString()));
            if (models != null && shown == fingerprint) return;
            Hide();
            if (food.Count == 0) return;
            if (!measured) { surface = Surface(); measured = true; }
            models = new GameObject("Shepherd trough food visuals"); models.transform.SetParent(transform, false);
            for (int index = 0; index < food.Count; index++)
            {
                var model = new GameObject("Food " + index); model.transform.SetParent(models.transform, false);
                var native = food[index].transform.Find("attach") ?? food[index].transform;
                var bounds = CopyMeshes(native, model.transform);
                if (!bounds.HasValue) { Destroy(model); continue; }
                float size = Mathf.Max(bounds.Value.size.x, bounds.Value.size.y, bounds.Value.size.z);
                float scale = Mathf.Min(1, 0.25f / size);
                model.transform.localScale = Vector3.one * scale;
                float offset = food.Count == 1 ? 0 : (index - (food.Count - 1) / 2f) * Mathf.Min(0.35f, surface.size.x / 4);
                var bottomCenter = new Vector3(bounds.Value.center.x, bounds.Value.min.y, bounds.Value.center.z);
                model.transform.localPosition = new Vector3(surface.center.x + offset, surface.max.y + 0.025f, surface.center.z) - bottomCenter * scale;
            }
            shown = fingerprint;
        }
        Bounds Surface()
        {
            var visual = GetComponentInParent<Piece>()?.transform.Find("meadworks_original_model");
            var minimum = visual != null ? visual.Find("food_surface_min") : null;
            var maximum = visual != null ? visual.Find("food_surface_max") : null;
            if (minimum != null && maximum != null)
            {
                var floor = new Bounds(transform.InverseTransformPoint(minimum.position), Vector3.zero);
                floor.Encapsulate(transform.InverseTransformPoint(maximum.position));
                return floor;
            }
            Bounds? combined = null;
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy
                    || models != null && filter.transform.IsChildOf(models.transform) || !Usable(filter)) continue;
                Include(ref combined, filter.sharedMesh.bounds, filter.transform, transform);
            }
            if (!combined.HasValue) throw new InvalidOperationException("native trough surface unavailable");
            return combined.Value;
        }
        static Bounds? CopyMeshes(Transform native, Transform target)
        {
            Bounds? combined = null;
            var lowerLods = new HashSet<Renderer>();
            foreach (var group in native.GetComponentsInChildren<LODGroup>(true))
            {
                var levels = group.GetLODs();
                for (int level = 1; level < levels.Length; level++) foreach (var renderer in levels[level].renderers)
                    if (renderer != null) lowerLods.Add(renderer);
                if (levels.Length > 0) foreach (var renderer in levels[0].renderers) lowerLods.Remove(renderer);
            }
            foreach (var filter in native.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || lowerLods.Contains(renderer) || !Usable(filter)) continue;
                var path = new List<Transform>();
                bool active = true;
                for (var cursor = filter.transform; cursor != native; cursor = cursor.parent)
                { path.Add(cursor); active &= cursor.gameObject.activeSelf; }
                if (!active) continue;
                Transform parent = target;
                for (int index = path.Count - 1; index >= 0; index--)
                {
                    var part = new GameObject(path[index].name); part.transform.SetParent(parent, false);
                    part.transform.localPosition = path[index].localPosition; part.transform.localRotation = path[index].localRotation;
                    part.transform.localScale = path[index].localScale; parent = part.transform;
                }
                parent.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                parent.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                Include(ref combined, filter.sharedMesh.bounds, parent, target);
            }
            return combined;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Usable(MeshFilter filter)
        {
            if (filter.sharedMesh == null) return false;
            var size = filter.sharedMesh.bounds.size;
            return Finite(size.x) && Finite(size.y) && Finite(size.z) && size.sqrMagnitude > 0.000001f;
        }
        static void Include(ref Bounds? combined, Bounds bounds, Transform source, Transform target)
        {
            foreach (float x in new[] { bounds.min.x, bounds.max.x }) foreach (float y in new[] { bounds.min.y, bounds.max.y }) foreach (float z in new[] { bounds.min.z, bounds.max.z })
            {
                var point = target.InverseTransformPoint(source.TransformPoint(new Vector3(x, y, z)));
                if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) throw new InvalidOperationException("invalid native visual bounds");
                var value = combined ?? new Bounds(point, Vector3.zero); value.Encapsulate(point); combined = value;
            }
        }
        void Hide() { if (models != null) { models.SetActive(false); Destroy(models); } models = null; shown = ""; }
        void OnDisable() => Hide();
        void OnDestroy() { Hide(); instances.Remove(this); }
        static void Track(Container container)
        {
            if (Plugin.AllowsUi && FeedSources.IsUsable(container) && TroughPieces.IsTrough(container) && container.GetComponent<TroughFoodVisuals>() == null)
                container.gameObject.AddComponent<TroughFoodVisuals>();
        }
        internal static void TrackLoaded() { if (Plugin.AllowsUi) foreach (var container in FeedSources.All) Track(container); }
        internal static void Clear() { foreach (var visual in new List<TroughFoodVisuals>(instances)) if (visual != null) { visual.Hide(); Destroy(visual); } instances.Clear(); }
        [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
        sealed class Loaded { [HarmonyPostfix] static void Postfix(Container __instance) => NativeFeeding.Run("trough food visuals", Track, __instance); }
    }
}

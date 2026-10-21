// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Original geometry and native API adapter; VAT's published feature description is a behavior reference only.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using CorePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using G = ExtraPotions.Hex.SpawnProtectionGeometry;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexSpawnProtection
{
    const float Radius = 50f, AnchorStep = 2f, TeleportDistance = 20f;
    const int CellsPerFrame = 256, MaximumAreas = 512, MaximumVertices = 220000;
    static readonly Color protectedColor = new(.15f, .75f, .36f, .27f);
    static readonly Color gapColor = new(.85f, .18f, .12f, .23f);
    static bool enabled;
    static Player? actor;
    static ZNet? network;
    static long generation = -1;
    static Vector3 previousPosition;
    static float refreshAt;
    static GameObject? root;
    static Mesh? mesh;
    static Material? material;
    static Builder? building;

    static bool Available => Plugin.Ready && Plugin.Active && Plugin.Settings?.Enabled.Value == true
        && Plugin.Settings.DebugTools.Value && MeadworksHeadless.AllowsUi && ZoneSystem.instance != null
        && HexMovement.Owned(Player.m_localPlayer) && ZNet.instance != null
        && CorePlugin.Session.AuthorityReady && CorePlugin.Session.WorldId != 0
        && MeadworksAuthority.ProductActive(Plugin.Guid, out _);
    static bool Allowed => Available && MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Information).Allowed;
    internal static bool IsEnabled => enabled;
    internal static string Toggle() => SetEnabled(!enabled);
    internal static string SetEnabled(bool value)
    {
        if (!value) { Reset(); return HexText.Text("spawn_protection_off"); }
        if (!Available) { Reset(); return HexText.Text("actions_unavailable"); }
        MeadworksMischief.Cheats.Tag(Plugin.Guid, "SpawnProtection", MischiefCategory.Information);
        if (!MeadworksMischief.TryUse(Plugin.Guid, "SpawnProtection", MischiefCategory.Information, HexText.Text("spawn_protection_label")))
        { Reset(); return HexText.Text("actions_denied"); }
        if (enabled) return HexText.Text("spawn_protection_on");
        var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
        if (shader == null) return HexText.Text("actions_unavailable");
        material = new Material(shader) { name = "Meadworks.Hex.SpawnProtection", hideFlags = HideFlags.HideAndDontSave, renderQueue = 3010 };
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)CullMode.Off); material.SetInt("_ZWrite", 0); material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        material.SetColor("_Color", Color.white);
        actor = Player.m_localPlayer; network = ZNet.instance; generation = CorePlugin.Session.Generation;
        previousPosition = actor.transform.position; refreshAt = 0; enabled = true;
        return HexText.Text("spawn_protection_on");
    }

    internal static void Reset()
    {
        enabled = false; building = null; ClearSurface();
        if (material != null) Object.Destroy(material);
        material = null; actor = null; network = null; generation = -1; refreshAt = 0;
    }
    static void ClearSurface()
    {
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        if (mesh != null) Object.Destroy(mesh);
        root = null; mesh = null;
    }
    internal static void Tick()
    {
        if (!enabled) return;
        if (!Allowed || actor != Player.m_localPlayer || network != ZNet.instance || generation != CorePlugin.Session.Generation)
        { Reset(); return; }
        var position = actor!.transform.position;
        if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) { Reset(); return; }
        if (actor.IsTeleporting() || (position - previousPosition).sqrMagnitude > TeleportDistance * TeleportDistance)
        {
            ClearSurface(); building = null; refreshAt = 0; previousPosition = position;
            if (actor.IsTeleporting()) return;
        }
        previousPosition = position;
        try
        {
            if (building == null && Time.unscaledTime >= refreshAt) building = new Builder(position);
            if (building == null) return;
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 500;
            int cells = 0;
            do { building.Step(); cells++; }
            while (!building.Done && cells < CellsPerFrame && Stopwatch.GetTimestamp() < deadline);
            if (!building.Done) return;
            Commit(building); building = null; refreshAt = Time.unscaledTime + 1.5f;
        }
        catch
        {
            Reset();
            if (MessageHud.instance != null) MessageHud.instance.ShowMessage(MessageHud.MessageType.Center, HexText.Text("actions_unavailable"));
        }
    }

    static void Commit(Builder finished)
    {
        if (finished.Vertices.Count == 0) { ClearSurface(); return; }
        var next = new Mesh { name = "Hex spawn protection terrain", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
        next.SetVertices(finished.Vertices); next.SetColors(finished.Colors); next.SetTriangles(finished.Triangles, 0); next.RecalculateBounds();
        if (root == null)
        {
            root = new GameObject("Meadworks.Hex.SpawnProtection", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        root.transform.position = finished.Anchor;
        root.GetComponent<MeshFilter>().sharedMesh = next;
        if (mesh != null) Object.Destroy(mesh);
        mesh = next;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    readonly struct Sphere
    {
        internal Sphere(Vector3 center, float radius) { Center = center; Radius = radius; }
        internal readonly Vector3 Center;
        internal readonly float Radius;
    }
    sealed class Builder
    {
        internal readonly Vector3 Anchor;
        internal readonly List<Vector3> Vertices = new();
        internal readonly List<Color> Colors = new();
        internal readonly List<int> Triangles = new();
        readonly G.Circle extent;
        readonly List<Sphere> spheres = new();
        readonly List<Collider> sampled = new();
        readonly List<G.Circle> circles = new();
        readonly List<Terrain> terrains = new();
        int terrainIndex;
        internal bool Done => terrainIndex >= terrains.Count;
        sealed class Terrain
        {
            internal readonly Heightmap Map;
            internal readonly Transform Transform;
            internal readonly Vector3[] Vertices;
            internal readonly int[] Indices;
            internal int Next;
            internal Terrain(Heightmap map, MeshCollider collider)
            {
                Map = map; Transform = collider.transform;
                Vertices = collider.sharedMesh.vertices; Indices = collider.sharedMesh.triangles;
                if (Vertices.Length > 66049 || Indices.Length > 393216 || Indices.Length % 3 != 0)
                    throw new InvalidOperationException("Loaded terrain mesh limit");
            }
        }
        internal Builder(Vector3 position)
        {
            Anchor = new Vector3(Mathf.Floor(position.x / AnchorStep) * AnchorStep, 0, Mathf.Floor(position.z / AnchorStep) * AnchorStep);
            extent = new G.Circle(position.x - Anchor.x, position.z - Anchor.z, Radius);
            foreach (var map in Heightmap.GetAllHeightmaps())
            {
                if (map == null || !map.isActiveAndEnabled || map.IsDistantLod || map.HaveQueuedRebuild()) continue;
                var collider = map.GetComponent<MeshCollider>();
                if (collider == null || !collider.enabled || collider.sharedMesh == null) continue;
                var bounds = collider.bounds;
                float dx = Mathf.Max(bounds.min.x - position.x, Mathf.Max(0, position.x - bounds.max.x));
                float dz = Mathf.Max(bounds.min.z - position.z, Mathf.Max(0, position.z - bounds.max.z));
                if (dx * dx + dz * dz > Radius * Radius) continue;
                if (terrains.Count >= 16) throw new InvalidOperationException("Nearby terrain limit");
                terrains.Add(new Terrain(map, collider));
            }
            var areas = EffectArea.GetAllAreas();
            if (areas.Count > 16384) throw new InvalidOperationException("Loaded protection area limit");
            int layer = LayerMask.NameToLayer("character_trigger");
            foreach (var area in areas)
            {
                if (area == null || !area.isActiveAndEnabled || (area.m_type & EffectArea.Type.PlayerBase) == 0) continue;
                var collider = area.GetComponent<Collider>();
                if (collider == null || !collider.enabled || collider.gameObject.layer != layer || !collider.gameObject.activeInHierarchy) continue;
                var bounds = collider.bounds;
                float dx = Mathf.Max(bounds.min.x - position.x, Mathf.Max(0, position.x - bounds.max.x));
                float dz = Mathf.Max(bounds.min.z - position.z, Mathf.Max(0, position.z - bounds.max.z));
                if (dx * dx + dz * dz > Radius * Radius) continue;
                if (spheres.Count + sampled.Count >= MaximumAreas) throw new InvalidOperationException("Nearby protection area limit");
                if (collider is SphereCollider sphere)
                {
                    var scale = sphere.transform.lossyScale;
                    float radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                    var center = sphere.transform.TransformPoint(sphere.center);
                    if (Finite(radius) && radius > 0 && radius <= 1024 && Finite(center.x) && Finite(center.y) && Finite(center.z))
                    { spheres.Add(new Sphere(center, radius)); continue; }
                }
                sampled.Add(collider);
            }
        }

        internal void Step()
        {
            if (Done) return;
            var terrain = terrains[terrainIndex];
            if (terrain.Map == null || !terrain.Map.isActiveAndEnabled || terrain.Map.HaveQueuedRebuild() || terrain.Next >= terrain.Indices.Length)
            { terrainIndex++; return; }
            var a = terrain.Transform.TransformPoint(terrain.Vertices[terrain.Indices[terrain.Next++]]);
            var b = terrain.Transform.TransformPoint(terrain.Vertices[terrain.Indices[terrain.Next++]]);
            var c = terrain.Transform.TransformPoint(terrain.Vertices[terrain.Indices[terrain.Next++]]);
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            double dxExtent = Math.Max(minX - Anchor.x - extent.X, Math.Max(0, extent.X - maxX + Anchor.x));
            double dzExtent = Math.Max(minZ - Anchor.z - extent.Z, Math.Max(0, extent.Z - maxZ + Anchor.z));
            if (dxExtent * dxExtent + dzExtent * dzExtent > Radius * Radius) return;
            var pa = new G.Point(a.x - Anchor.x, a.z - Anchor.z); var pb = new G.Point(b.x - Anchor.x, b.z - Anchor.z); var pc = new G.Point(c.x - Anchor.x, c.z - Anchor.z);
            if (Math.Abs((pb.X - pa.X) * (pc.Z - pa.Z) - (pb.Z - pa.Z) * (pc.X - pa.X)) <= 1e-9) return;
            var middle = (a + b + c) / 3;
            bool blocked = ZoneSystem.instance.IsBlocked(middle);
            foreach (var shape in sampled)
                if (shape != null && shape.enabled && shape.gameObject.activeInHierarchy && (shape.ClosestPoint(middle) - middle).sqrMagnitude < 1e-8f)
                { blocked = true; break; }
            circles.Clear();
            if (!blocked)
            {
                float low = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), high = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                foreach (var sphere in spheres)
                {
                    float vertical = Mathf.Max(Mathf.Abs(low - sphere.Center.y), Mathf.Abs(high - sphere.Center.y));
                    float squared = sphere.Radius * sphere.Radius - vertical * vertical;
                    float dx = Mathf.Max(minX - sphere.Center.x, Mathf.Max(0, sphere.Center.x - maxX));
                    float dz = Mathf.Max(minZ - sphere.Center.z, Mathf.Max(0, sphere.Center.z - maxZ));
                    if (squared > dx * dx + dz * dz)
                        circles.Add(new G.Circle(sphere.Center.x - Anchor.x, sphere.Center.z - Anchor.z, Math.Sqrt(squared)));
                }
            }
            // Each fragment stays on an actual loaded terrain triangle, including its native scale and diagonal.
            var regions = G.PartitionTriangle(pa, pb, pc, circles, extent, blocked);
            foreach (var region in regions)
            {
                if (Vertices.Count + region.Points.Count > MaximumVertices) throw new InvalidOperationException("Protection mesh limit");
                int first = Vertices.Count;
                foreach (var point in region.Points)
                {
                    float height = (float)G.TriangleHeight(point, pa, a.y, pb, b.y, pc, c.y);
                    Vertices.Add(new Vector3((float)point.X, height + .08f, (float)point.Z));
                    Colors.Add(region.Covered ? protectedColor : gapColor);
                }
                for (int vertex = 1; vertex + 1 < region.Points.Count; vertex++)
                { Triangles.Add(first); Triangles.Add(first + vertex + 1); Triangles.Add(first + vertex); }
            }
        }
    }
}

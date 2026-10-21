// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API adapter. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Hex.Runtime.Native;

internal sealed class HexPoiEstimate : IDisposable
{
    static readonly ConditionalWeakTable<GameObject, object> owned = new();
    readonly ZoneSystem live;
    readonly WorldGenerator generator;
    readonly World world;
    readonly AltBiomeWorldData biome;
    readonly List<ZoneSystem.ZoneLocation> definitions;
    readonly ZoneSystem.ZoneLocation[] ordered;
    readonly int definitionCount;
    readonly Stopwatch lifetime = Stopwatch.StartNew(), slice = new();
    GameObject? node;
    ZoneSystem? sandbox;
    IEnumerator? step;
    int index;
    bool disposed;
    internal int Completed => index;
    internal int Total => ordered.Length;

    internal HexPoiEstimate()
    {
        live = ZoneSystem.instance;
        generator = WorldGenerator.instance;
        world = ZNet.World;
        if (live == null || generator == null || world?.m_biomeData?.IsReady != true || !ReferenceEquals(generator.m_world, world)
            || live.m_generateLocationsCoroutine != null || live.m_locations == null
            || live.m_locations.Count == 0 || live.m_locations.Count > 4096)
            throw new InvalidOperationException("poi_estimate_unavailable");
        definitions = live.m_locations;
        definitionCount = definitions.Count;
        biome = world.m_biomeData;
        // Stable native priority order, including non-POIs that affect spacing and group constraints.
        ordered = definitions.Where(location => location != null && location.m_enable && location.m_quantity != 0)
            .OrderByDescending(location => location.m_prioritized).ToArray();
        try
        {
            node = new GameObject("HexPoiEstimate");
            node.SetActive(false);
            owned.Add(node, new object());
            sandbox = node.AddComponent<ZoneSystem>();
            sandbox.enabled = false;
            sandbox.m_timeSlicedGenerationTimeBudget = 0.002f;
        }
        catch { Dispose(); throw; }
    }

    internal bool Tick()
    {
        if (disposed || sandbox == null || node == null || node.activeInHierarchy || live != ZoneSystem.instance
            || generator != WorldGenerator.instance || !ReferenceEquals(world, ZNet.World) || !ReferenceEquals(generator.m_world, world)
            || !ReferenceEquals(world.m_biomeData, biome) || !biome.IsReady || !ReferenceEquals(definitions, live.m_locations)
            || definitions.Count != definitionCount
            || live.m_generateLocationsCoroutine != null || lifetime.Elapsed.TotalSeconds > 120)
            throw new InvalidOperationException("poi_estimate_unavailable");
        if (index == ordered.Length) return true;
        var random = UnityEngine.Random.state;
        try
        {
            slice.Restart();
            step ??= sandbox.GenerateLocationsTimeSliced(ordered[index], slice, new ZPackage());
            // Native yields retain insideState and resume it before sampling; its returned RNG is external.
            // Exactly one synchronous native step per frame; finally additionally covers faults/disposal.
            if (!step.MoveNext()) { DisposeStep(); index++; }
        }
        finally { UnityEngine.Random.state = random; }
        return index == ordered.Length;
    }

    internal IReadOnlyList<Tuple<string, float, float, float, bool>> Read()
    {
        if (disposed || index != ordered.Length || sandbox == null) throw new InvalidOperationException("poi_estimate_unavailable");
        return HexPoiRows.Select(sandbox.m_locationInstances.Values.Select(location => Tuple.Create(
            location.m_location?.m_prefabName ?? "", location.m_position.x, location.m_position.y, location.m_position.z, false)));
    }

    void DisposeStep()
    {
        var iterator = step; step = null;
        var random = UnityEngine.Random.state;
        try { (iterator as IDisposable)?.Dispose(); }
        finally { UnityEngine.Random.state = random; }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { DisposeStep(); }
        finally
        {
            var singleton = ZoneSystem.instance;
            // Immediate owned teardown runs before Plugin.Shutdown removes the lifecycle guard.
            if (node != null && node.transform.childCount == 0) UnityEngine.Object.DestroyImmediate(node);
            else if (sandbox != null)
            {
                UnityEngine.Object.DestroyImmediate(sandbox);
                UnityEngine.Debug.LogWarning("Hex estimate retained an inactive owner with external children.");
            }
            sandbox = null;
            node = null;
            if (!ReferenceEquals(singleton, ZoneSystem.instance))
            {
                UnityEngine.Debug.LogError("Hex estimate teardown changed the native singleton.");
                throw new InvalidOperationException("poi_estimate_singleton_changed");
            }
        }
    }

    [HarmonyPatch]
    static class OwnedLifecycle
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var name in new[] { "Awake", "Start", "Update", "OnDestroy" })
                yield return AccessTools.DeclaredMethod(typeof(ZoneSystem), name);
        }
        [HarmonyPriority(Priority.First)]
        static bool Prefix(ZoneSystem __instance) => !owned.TryGetValue(__instance.gameObject, out _);
    }
}

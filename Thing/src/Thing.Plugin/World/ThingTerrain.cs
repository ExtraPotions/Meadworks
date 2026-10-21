// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions from aedenthorn/ValheimMods TerrainReset/ResetTerrain, public-domain dedication.
// Pin 83690e8ef9a37c4694604416667d252b4ff5f645. Bounded clearing/save algorithm adapted; original plugin/input not adopted.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Thing.Admin;
using ExtraPotions.Thing.WorldAdmin;
using UnityEngine;

namespace ExtraPotions.Thing.Runtime
{
    /// <summary>Authority-authorized terrain adapter. Preflight all data and ownership before the first write; never claim another peer's terrain.</summary>
    public static class ThingTerrain
    {
        public const string Incomplete = "terrain_incomplete", Ownership = "terrain_ownership", OtherPieces = "terrain_other_pieces";

        public static string? Check(Vector3 center, int radius, Verdict verdict, long requester, bool requireOwner = false, bool serverOnly = true)
        {
            if (radius < 1 || radius > 50 || !Finite(center.x) || !Finite(center.y) || !Finite(center.z)) return Incomplete;
            var outcome = TerrainReset.Decide(verdict, requester, center.x, center.z, radius, Area(center, radius));
            if (outcome == TerrainOutcome.RefusedOtherPieces) return OtherPieces;
            if (outcome == TerrainOutcome.RefusedNotAdmin) return AdminCheck.TextKey(Verdict.NotAdmin);
            if (outcome != TerrainOutcome.Proceed) return Incomplete;
            var refused = Collect(center, radius, out _, out _, requireOwner, serverOnly);
            if (refused != null) return refused;
            return requireOwner || Owner(center, radius) != 0 ? null : Ownership;
        }

        /// <summary>Single current owner only. Multi-owner resets are refused rather than partially clearing an area.</summary>
        public static long Owner(Vector3 center, int radius)
        {
            if (Collect(center, radius, out var maps, out var modifiers, false, false) != null) return 0;
            var owners = new HashSet<long>();
            foreach (var map in maps)
            {
                var comp = TerrainComp.FindTerrainCompiler(map.transform.position);
                if (comp) owners.Add(comp.m_nview.GetZDO().GetOwner());
            }
            foreach (var modifier in modifiers) owners.Add(modifier.GetComponent<ZNetView>().GetZDO().GetOwner());
            return owners.Count == 0 ? ZNet.GetUID() : owners.Count == 1 ? owners.First() : 0;
        }

        public static string? Reset(Vector3 center, int radius, Verdict verdict, long requester, out int changes, bool serverOnly = true)
        {
            changes = 0;
            var refused = Check(center, radius, verdict, requester, true, serverOnly);
            if (refused != null) return refused;
            refused = Collect(center, radius, out var maps, out var modifiers, true, serverOnly);
            if (refused != null) return refused;
            // Main-thread synchronous operation: no yields between all-owner preflight and native writes.
            foreach (var modifier in modifiers)
            {
                var view = modifier.GetComponent<ZNetView>();
                if (!Owned(view)) return Ownership;
                foreach (var map in maps) if (map.TerrainVSModifier(modifier)) map.Poke();
                view.Destroy();
                changes++;
            }
            foreach (var map in maps)
            {
                var comp = TerrainComp.FindTerrainCompiler(map.transform.position);
                if (!comp) continue; // No compiler means no compiled edits on this loaded heightmap.
                if (!Owned(comp.m_nview)) return Ownership;
                // Keep a fractional grid center so rounding cannot clear cells outside the approved world-space circle.
                var x = (center.x - map.transform.position.x) / map.m_scale + comp.m_width * 0.5f;
                var y = (center.z - map.transform.position.z) / map.m_scale + comp.m_width * 0.5f;
                var changed = false;
                foreach (var index in TerrainCells.InRadius(comp.m_width + 1, x, y, radius, map.m_scale))
                    if (comp.m_modifiedHeight[index])
                    {
                        comp.m_modifiedHeight[index] = false;
                        comp.m_levelDelta[index] = 0;
                        comp.m_smoothDelta[index] = 0;
                        changed = true;
                        changes++;
                    }
                // Current native paint buffers have (width + 1)^2 entries, unlike the old upstream width^2 loop.
                var paintOrigin = map.VertexMaskToWorld(0, 0);
                x = (center.x - paintOrigin.x) / map.m_scale;
                y = (center.z - paintOrigin.z) / map.m_scale;
                foreach (var index in TerrainCells.InRadius(comp.m_width + 1, x, y, radius, map.m_scale))
                    if (comp.m_modifiedPaint[index])
                    {
                        comp.m_modifiedPaint[index] = false;
                        comp.m_paintMask[index] = Color.clear;
                        changed = true;
                        changes++;
                    }
                if (!changed) continue;
                comp.Save(false);
                map.Poke();
            }
            if (changes > 0 && ClutterSystem.instance) ClutterSystem.instance.ResetGrass(center, radius);
            return null;
        }

        static string? Collect(Vector3 center, int radius, out List<Heightmap> maps, out List<TerrainModifier> modifiers, bool requireOwner, bool serverOnly)
        {
            maps = new List<Heightmap>();
            modifiers = new List<TerrainModifier>();
            if (!ZNet.instance || (serverOnly && !ZNet.instance.IsServer()) || !Loaded(center, radius)) return Incomplete;
            Heightmap.FindHeightmap(center, radius, maps);
            if (maps.Count == 0) return Incomplete;
            foreach (var map in maps)
            {
                if (!map || map.IsDistantLod || !Finite(map.m_scale) || map.m_scale <= 0) return Incomplete;
                var comp = TerrainComp.FindTerrainCompiler(map.transform.position);
                if (!comp) continue;
                if (!comp.m_initialized || comp.m_width != map.m_width || !TerrainCells.Shape(comp.m_width + 1,
                    comp.m_modifiedHeight?.Length ?? 0, comp.m_levelDelta?.Length ?? 0, comp.m_smoothDelta?.Length ?? 0,
                    comp.m_modifiedPaint?.Length ?? 0, comp.m_paintMask?.Length ?? 0)) return Incomplete;
                if (!comp.m_nview || !comp.m_nview.IsValid()) return Incomplete;
                if (comp.m_nview.GetZDO().GetOwner() == 0 || (requireOwner && !Owned(comp.m_nview))) return Ownership;
            }
            foreach (var modifier in TerrainModifier.GetAllInstances())
            {
                if (!modifier) continue;
                var distance = Utils.DistanceXZ(modifier.transform.position, center);
                if (!Finite(distance)) return Incomplete;
                // A legacy modifier is indivisible. Bound even square footprints conservatively; never erase edits outside the preview.
                var extent = modifier.GetRadius() * Mathf.Sqrt(2);
                if (!Finite(extent) || extent < 0) return Incomplete;
                if (distance > radius + extent) continue;
                if (distance + extent > radius) return Incomplete;
                var view = modifier.GetComponent<ZNetView>();
                if (!view || !view.IsValid()) return Incomplete;
                if (view.GetZDO().GetOwner() == 0 || (requireOwner && !Owned(view))) return Ownership;
                modifiers.Add(modifier);
            }
            return null;
        }

        static bool Owned(ZNetView? view) => view != null && view.IsValid() && view.IsOwner();
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static bool Loaded(Vector3 center, int radius)
        {
            var zones = ZoneSystem.instance;
            if (!zones) return false;
            var low = ZoneSystem.GetZone(center - new Vector3(radius, 0, radius));
            var high = ZoneSystem.GetZone(center + new Vector3(radius, 0, radius));
            for (var z = (int)low.y; z <= high.y; z++)
                for (var x = (int)low.x; x <= high.x; x++)
                    if (!zones.IsZoneLoaded(new Vector2s(x, z))) return false;
            return true;
        }

        static TerrainArea Area(Vector3 center, int radius)
        {
            if (!Loaded(center, radius)) return new TerrainArea(false, Array.Empty<PieceFootprint>());
            var pieces = new List<PieceFootprint>();
            foreach (var piece in Piece.s_allPieces)
            {
                if (!piece) continue;
                var colliders = piece.GetComponentsInChildren<Collider>().Where(c => c && c.enabled).ToArray();
                if (colliders.Length == 0)
                {
                    // An unmeasurable nearby piece cannot be classified safely for an own-pieces reset.
                    if (Utils.DistanceXZ(piece.transform.position, center) <= radius + 50) return new TerrainArea(false, pieces);
                    continue;
                }
                var bounds = colliders[0].bounds;
                foreach (var collider in colliders.Skip(1)) bounds.Encapsulate(collider.bounds);
                var extent = new Vector2(bounds.extents.x, bounds.extents.z).magnitude;
                if (!Finite(bounds.center.x) || !Finite(bounds.center.z) || !Finite(extent)) return new TerrainArea(false, pieces);
                pieces.Add(new PieceFootprint(bounds.center.x, bounds.center.z, extent, piece.GetCreator()));
            }
            return new TerrainArea(true, pieces);
        }
    }
}

// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (user parity rows 26 and 27, 2026-10-10): the admin surface reset. Valheim_Admin_Tool's "reset
// resources" and "regenerate nature" are behavior references only (no code used). The game's own vegetation generator
// says what a generated zone held; these rules decide which of those spots come back.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Regrow;

namespace ExtraPotions.Delve.Surface
{
    /// <summary>What an admin's surface reset brings back.</summary>
    public enum SurfaceScope { Resources, Nature, All }

    /// <summary>What a generated vegetation prefab is, read from its parts.</summary>
    public enum SurfaceClass { None, Tree, Bush, Mineable, Pickable }

    /// <summary>The decision for one generated spot.</summary>
    public enum SurfaceSpot { Present, Base, Location, Player, Restore }

    /// <summary>What the rules need to know about one generated spot now. The plugin fills it from the world.</summary>
    public sealed class SurfaceFacts
    {
        public SurfaceFacts(Vec3 spot, bool present, float baseClearance)
        {
            if (!(baseClearance >= 0) || float.IsInfinity(baseClearance)) throw new ArgumentException("Expected a finite clearance of 0 or more.", nameof(baseClearance));
            Spot = spot;
            Present = present;
            BaseClearance = baseClearance;
        }

        public Vec3 Spot { get; }

        /// <summary>An object of the same prefab (or its partly mined form) stands at the spot.</summary>
        public bool Present { get; }

        /// <summary>Surface / Surface Base Clearance in meters; 0 turns the player-built check off.</summary>
        public float BaseClearance { get; }

        /// <summary>Positions of anything a player built near the spot.</summary>
        public IReadOnlyList<Vec3> Built { get; set; } = Array.Empty<Vec3>();

        /// <summary>Player base and ward areas near the spot.</summary>
        public IReadOnlyList<Area> Bases { get; set; } = Array.Empty<Area>();

        /// <summary>Exterior circles of the world locations near the spot.</summary>
        public IReadOnlyList<Area> Locations { get; set; } = Array.Empty<Area>();

        public IReadOnlyList<Vec3> Players { get; set; } = Array.Empty<Vec3>();
    }

    public static class SurfaceRules
    {
        /// <summary>The generator puts an object at the same flat spot every time; a standing object this close is the same one.</summary>
        public const float MatchRadius = 0.75f;

        /// <summary>Nothing is placed this close (flat) to a player.</summary>
        public const float PlayerRadius = 3f;

        /// <summary>A felled tree's stump this close to a returning tree is removed with it.</summary>
        public const float StubRadius = 1.5f;

        /// <summary>Share of the standing objects of a prefab the regenerated layout must find before any of that prefab returns.</summary>
        public const double LayoutShare = 0.75;

        public static bool InScope(SurfaceScope scope, SurfaceClass kind)
        {
            if (kind == SurfaceClass.None) return false;
            switch (scope)
            {
                case SurfaceScope.Resources: return kind == SurfaceClass.Mineable || kind == SurfaceClass.Pickable;
                case SurfaceScope.Nature: return kind == SurfaceClass.Tree || kind == SurfaceClass.Bush;
                default: return true;
            }
        }

        /// <summary>
        /// A tree (TreeBase), a mineable rock or ore (MineRock, MineRock5, or a destructible that breaks into one), a
        /// pickable (flint, stones, branches, berries, mushrooms), or another destructible (bushes, small rocks).
        /// Prefabs with an excluded part (creature, container, spawner, ship) are never touched.
        /// </summary>
        public static SurfaceClass Classify(bool tree, bool mineable, bool pickable, bool destructible, bool excluded)
        {
            if (excluded) return SurfaceClass.None;
            if (tree) return SurfaceClass.Tree;
            if (mineable) return SurfaceClass.Mineable;
            if (pickable) return SurfaceClass.Pickable;
            return destructible ? SurfaceClass.Bush : SurfaceClass.None;
        }

        /// <summary>
        /// One generated spot: a standing object stays; nothing returns within the base clearance of anything a player
        /// built or inside a base or ward area, inside a location's exterior, or on a player; otherwise it is restored.
        /// </summary>
        public static SurfaceSpot Decide(SurfaceFacts facts)
        {
            if (facts == null) throw new ArgumentNullException(nameof(facts));
            if (facts.Present) return SurfaceSpot.Present;
            if (BuiltWithin(facts.Spot, facts.BaseClearance, facts.Built) || facts.Bases.Any(a => a.Contains(facts.Spot))) return SurfaceSpot.Base;
            if (facts.Locations.Any(a => a.Contains(facts.Spot))) return SurfaceSpot.Location;
            if (facts.Players.Any(p => Vec3.Flat(p, facts.Spot) < PlayerRadius)) return SurfaceSpot.Player;
            return SurfaceSpot.Restore;
        }

        /// <summary>
        /// Anything a player built within the clearance, flat, at any height on the surface (a hill above counts too);
        /// pieces in a dungeon interior far above or below do not. A clearance of 0 turns the test off.
        /// </summary>
        public static bool BuiltWithin(Vec3 spot, float clearance, IEnumerable<Vec3> built)
        {
            if (clearance <= 0f || built == null) return false;
            foreach (var p in built)
                if (Vec3.Flat(p, spot) < clearance && Math.Abs(p.Y - spot.Y) < InteriorGap) return true;
            return false;
        }

        /// <summary>Dungeon interiors stand thousands of meters above their entrance; this height gap separates them from the surface.</summary>
        public const float InteriorGap = 1000f;

        public static bool Matches(Vec3 generated, Vec3 standing) => Vec3.Flat(generated, standing) < MatchRadius;

        /// <summary>
        /// The regenerated layout of a prefab may be used in a zone only when it finds at least three quarters of the
        /// objects of that prefab still standing there. Otherwise the zone has changed since it was generated (or another
        /// mod moved things) and restoring would add objects beside the old ones.
        /// </summary>
        public static bool LayoutMatches(int standing, int matched) => standing <= 0 || matched >= Math.Ceiling(standing * LayoutShare);

        /// <summary>The zones a circle of <paramref name="radius"/> meters around the center touches, nearest first.</summary>
        public static IReadOnlyList<(int X, int Z)> ZonesWithin(Vec3 center, float radius)
        {
            var home = RegrowRules.Zone(center);
            int reach = (int)Math.Ceiling(radius / RegrowRules.ZoneSize) + 1;
            var zones = new List<(int X, int Z)>();
            for (int dx = -reach; dx <= reach; dx++)
                for (int dz = -reach; dz <= reach; dz++)
                {
                    var zone = (home.X + dx, home.Z + dz);
                    if (RegrowRules.ZoneNear(zone, center, radius)) zones.Add(zone);
                }
            return zones.OrderBy(z => Math.Abs(z.Item1 - home.X) + Math.Abs(z.Item2 - home.Z)).ThenBy(z => z.Item1).ThenBy(z => z.Item2).ToList();
        }
    }

    /// <summary>What one surface reset did, for the admin's reply and the server log.</summary>
    public sealed class SurfaceReport
    {
        public SurfaceReport(SurfaceScope scope, bool dryRun)
        {
            Scope = scope;
            DryRun = dryRun;
        }

        public SurfaceScope Scope { get; }
        public bool DryRun { get; }

        /// <summary>Generated, loaded zones the reset went through.</summary>
        public int Zones { get; set; }

        /// <summary>Zones in range left out because they are not generated or not loaded on the server.</summary>
        public int ZonesSkipped { get; set; }

        public int Restored { get; set; }

        /// <summary>Stumps removed where a tree came back.</summary>
        public int Stubs { get; set; }

        /// <summary>Prefab layouts skipped in a zone because they no longer match what stands there.</summary>
        public int Diverged { get; private set; }

        /// <summary>Prefabs whose layout was left as it is in at least one zone.</summary>
        public SortedSet<string> DivergedNames { get; } = new SortedSet<string>(StringComparer.Ordinal);

        public void Diverge(string prefab)
        {
            Diverged++;
            DivergedNames.Add(prefab);
        }

        public Dictionary<SurfaceSpot, int> Held { get; } = new Dictionary<SurfaceSpot, int>();

        /// <summary>Restored counts by prefab name.</summary>
        public SortedDictionary<string, int> ByPrefab { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<string> Lines()
        {
            var what = Scope == SurfaceScope.Resources ? "resources" : Scope == SurfaceScope.Nature ? "nature" : "resources and nature";
            var lines = new List<string>
            {
                "Delve surface reset (" + what + ")" + (DryRun ? ", dry run: nothing was changed" : "") + ": "
                    + N(Restored) + (DryRun ? " would come back" : " came back") + " in " + N(Zones) + " zones"
                    + (Stubs > 0 ? ", " + N(Stubs) + " stumps cleared" : "") + ".",
            };
            var held = new List<string>();
            if (Held.TryGetValue(SurfaceSpot.Base, out var b) && b > 0) held.Add(N(b) + " inside the base clearance");
            if (Held.TryGetValue(SurfaceSpot.Location, out var l) && l > 0) held.Add(N(l) + " inside locations");
            if (Held.TryGetValue(SurfaceSpot.Player, out var p) && p > 0) held.Add(N(p) + " next to a player");
            if (held.Count > 0) lines.Add("Held back: " + string.Join(", ", held) + ".");
            if (ZonesSkipped > 0) lines.Add(N(ZonesSkipped) + " zones in range are not generated or not loaded on the server and were left out.");
            if (Diverged > 0) lines.Add(N(Diverged) + " object layouts no longer match their zone and were left as they are (" + string.Join(", ", DivergedNames) + ").");
            return lines;
        }

        private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

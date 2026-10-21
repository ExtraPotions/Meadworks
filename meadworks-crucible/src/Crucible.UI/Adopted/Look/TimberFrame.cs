// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/TimberFrame.cs and TimberCoordinates.cs at
// 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: the geometry moved to Core TimberGeometry (same float
// math, tested portably); this file converts Unity vectors and keeps the settings version that live tuning bumps.
// TimberCoordinates.Uv is folded in as Uv.
// Modified by expDARE, 2026-10-13: Contour and BevelWidth take the roughness (0 draws a straight edge for forged kit windows).
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>One continuous wood silhouette and bevel; no separate border texture or metal band.</summary>
    public static class TimberFrame
    {
        private static readonly List<KitVector> points = new List<KitVector>();
        private static readonly List<KitVector> directions = new List<KitVector>();
        private static readonly List<KitVector> polygon = new List<KitVector>();
        private static bool pending;

        public static float Width => KitLook.Width;

        internal static float Roughness => KitLook.Jaggedness;

        public static int Version { get; private set; }

        public static float BevelWidth(Rect rect, bool round) => BevelWidth(rect, round, Roughness);

        public static float BevelWidth(Rect rect, bool round, float roughness) => TimberGeometry.BevelWidth(KitUnity.Rect(rect), round, Width, roughness);

        /// <summary>A border setting changed: every Timber panel rebuilds its outline on its next frame.</summary>
        public static void Invalidate() => pending = true;

        public static void Poll()
        {
            if (!pending) return;
            pending = false;
            Version++;
        }

        // Recognize previous preview sprites when restoring an existing panel.
        public static bool Owns(Sprite sprite) => sprite != null &&
            (sprite.name == KitThemeRules.FrameName || sprite.name == KitArtCatalog.SpriteName("ringframe"));

        public static void Contour(Rect rect, bool round, List<Vector2> contour, List<Vector2> normals) => Contour(rect, round, Roughness, contour, normals);

        public static void Contour(Rect rect, bool round, float roughness, List<Vector2> contour, List<Vector2> normals)
        {
            TimberGeometry.Contour(KitUnity.Rect(rect), round, roughness, points, directions);
            contour.Clear();
            normals.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                contour.Add(KitUnity.Vector(points[i]));
                normals.Add(KitUnity.Vector(directions[i]));
            }
        }

        public static void Triangulate(List<Vector2> interior, List<int> triangles)
        {
            polygon.Clear();
            foreach (Vector2 point in interior)
                polygon.Add(new KitVector(point.x, point.y));
            TimberGeometry.Triangulate(polygon, triangles);
        }

        /// <summary>A single aspect-preserving wood image covering the root canvas, anchored at its top-left.</summary>
        public static Vector2 Uv(Vector2 point, Rect canvas, Rect wood, Vector2 textureSize) =>
            KitUnity.Vector(TimberGeometry.WallpaperUv(new KitVector(point.x, point.y), KitUnity.Rect(canvas), KitUnity.Rect(wood),
                new KitVector(textureSize.x, textureSize.y)));
    }
}

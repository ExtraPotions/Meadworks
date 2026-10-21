// Threshold - ExtraPotions Meadworks portals: sea gates.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/SeaGates/SeaGateSurfaceSheet.cs.
// Changed by expDARE, 2026: Threshold namespace; Threshold's settings, log, catalog words, access rules and portal keys (SeaGateBridge); Threshold ZDO keys, RPC names and pillar prefab name; patches installed by Threshold's own Harmony instance; the Kit picker instead of the large map; no crew pointers, map icons, placement preview or copied portal swirl.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>The sheet of a gate's surface: a flat mesh across the whole rectangle with soft edges, wearing a
    /// tileable ripple texture made here, in two layers that drift against each other so the surface shimmers. It makes
    /// the gate read as one surface even where the particles are thin or far away, and it is the whole look when the
    /// game's portal effect cannot be found. Unlit, alpha-blended and drawn from both sides (the shaders used have no
    /// back-face culling); alpha carries the fade, through the vertex colours.</summary>
    internal sealed class SeaGateSurfaceSheet
    {
        private const int Layers = 2;
        private const int Grid = 4;                    // vertices per row and per column of one layer
        private const int PerLayer = Grid * Grid;
        private const float EdgeFade = 1.2f;           // metres over which the sheet fades out at its edges
        private const float LayerAlpha = 0.6f;
        private const float PulseDepth = 0.2f;
        private const float PulseSpeed = 1.6f;
        private static readonly float[] LayerTile = { 5f, 7.5f };   // metres per texture repeat
        private static readonly Vector2[] LayerScroll = { new Vector2(0.03f, 0.055f), new Vector2(-0.045f, 0.025f) };
        private static readonly Color PortalTint = new Color(0.05f, 0.035f, 0.03f, 0.45f);  // under the game's swirl
        private static readonly Color PlainTint = new Color(0.62f, 0.85f, 1f, 0.6f);        // alone: blue-white

        private readonly Mesh mesh;
        private readonly MeshRenderer renderer;
        private readonly Color tint;
        private readonly Vector3[] positions = new Vector3[Layers * PerLayer];
        private readonly Vector2[] baseUv = new Vector2[Layers * PerLayer];
        private readonly float[] edgeAlpha = new float[Layers * PerLayer];
        private readonly List<Vector2> uvs = new List<Vector2>(new Vector2[Layers * PerLayer]);
        private readonly List<Color> colors = new List<Color>(new Color[Layers * PerLayer]);

        private SeaGateSurfaceSheet(Mesh mesh, MeshRenderer renderer, Color tint)
        {
            this.mesh = mesh;
            this.renderer = renderer;
            this.tint = tint;
        }

        /// <summary>A sheet across the frame under <paramref name="root"/>; null when no usable shader is loaded.
        /// <paramref name="underEffect"/>: the game's swirl is drawn over it, so the sheet is a dark veil, not the
        /// blue-white surface it is on its own.</summary>
        internal static SeaGateSurfaceSheet Build(Transform root, SeaGateSurfaceFrame frame, bool underEffect)
        {
            Material material = SeaGateSurfaceLook.SheetMaterial;
            if (material == null)
                return null;
            GameObject go = new GameObject("Threshold_SeaGateSheet");
            go.transform.SetParent(root, false);
            Mesh mesh = new Mesh { name = "Threshold_SeaGateSheet" };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            SeaGateSurfaceSheet sheet = new SeaGateSurfaceSheet(mesh, renderer, underEffect ? PortalTint : PlainTint);
            sheet.Shape(frame.Width, frame.Height);
            return sheet;
        }

        /// <summary>Scrolls the layers and sets the alpha; every frame, 32 vertices, but only while some camera drew
        /// the sheet last frame (a gate behind the player or out of view uploads nothing; it picks up the scroll from
        /// the time when it comes back into view).</summary>
        internal void Animate(float time, float intensity)
        {
            bool on = intensity > 0f;
            if (renderer.enabled != on)
                renderer.enabled = on;
            if (!on || !renderer.isVisible)
                return;
            float pulse = 1f - PulseDepth * (0.5f + 0.5f * Mathf.Sin(time * PulseSpeed));
            for (int i = 0; i < baseUv.Length; i++)
            {
                Vector2 scroll = LayerScroll[i / PerLayer] * time;
                uvs[i] = baseUv[i] + new Vector2(Mathf.Repeat(scroll.x, 1f), Mathf.Repeat(scroll.y, 1f));
                Color color = tint;
                color.a *= edgeAlpha[i] * intensity * pulse;
                colors[i] = color;
            }
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
        }

        internal void Destroy()
        {
            if (mesh != null)
                Object.Destroy(mesh);
        }

        private void Shape(float width, float height)
        {
            float[] xs = Steps(width);
            float[] ys = Steps(height);
            for (int i = 0; i < positions.Length; i++)
            {
                int layer = i / PerLayer;
                int row = i % PerLayer / Grid;
                int column = i % Grid;
                positions[i] = new Vector3(xs[column], ys[row], 0f);
                baseUv[i] = new Vector2(xs[column], ys[row]) / LayerTile[layer];
                bool inner = row > 0 && row < Grid - 1 && column > 0 && column < Grid - 1;
                edgeAlpha[i] = inner ? LayerAlpha : 0f;
            }
            mesh.MarkDynamic();
            mesh.vertices = positions;
            mesh.uv = baseUv;
            mesh.SetColors(colors);
            mesh.triangles = Triangles();
            mesh.RecalculateBounds();
        }

        /// <summary>The grid lines along one side: the outer edge, the end of the edge fade, and the same mirrored.</summary>
        private static float[] Steps(float size)
        {
            float half = size * 0.5f;
            float fade = Mathf.Min(EdgeFade, size * 0.25f);
            return new[] { -half, -half + fade, half - fade, half };
        }

        private static int[] Triangles()
        {
            List<int> triangles = new List<int>(Layers * (Grid - 1) * (Grid - 1) * 6);
            for (int layer = 0; layer < Layers; layer++)
            {
                for (int row = 0; row < Grid - 1; row++)
                {
                    for (int column = 0; column < Grid - 1; column++)
                    {
                        int a = layer * PerLayer + row * Grid + column;
                        triangles.AddRange(new[] { a, a + Grid, a + Grid + 1, a, a + Grid + 1, a + 1 });
                    }
                }
            }
            return triangles.ToArray();
        }
    }
}

// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/TimberBackground.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: marker and rim names from KitThemeRules, tint from
// KitPalette, ring depth/shade/alpha from Core TimberGeometry; the round shape (PackPanel: the key ring pop-up by name)
// is any image registered with RegisterRound (by name or instance).
// Modified by expDARE, 2026-10-13: an image registered with RegisterForged (kit windows, the forged set) shows the calm
// window wood (KitForgedArt.WindowWood) inside a straight edge, for the forged-iron frame laid over it.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A panel-sized window onto canvas-aligned wood. The fill and softly chipped bevel form one mesh;
    /// transparent silhouette cuts affect only this surface, never the panel's controls or icons.
    /// </summary>
    public sealed class TimberBackground : MaskableGraphic
    {
        private const string Name = KitThemeRules.WoodMarker;
        private static readonly Color PanelTint = KitUnity.Colour(KitPalette.PanelTint);
        private static readonly HashSet<string> roundNames = new HashSet<string>();
        private static readonly HashSet<Image> roundImages = new HashSet<Image>();
        private static readonly HashSet<Image> forgedImages = new HashSet<Image>();
        private Image frame;
        private Sprite source;
        private bool originalFill;
        private Sprite originalSprite;
        private Material originalMaterial;
        private Image.Type originalType;
        private Color originalColor;
        private bool round;
        private bool straight;
        private readonly List<Vector2> contour = new List<Vector2>();
        private readonly List<Vector2> normals = new List<Vector2>();
        private readonly List<Vector2> interior = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private Matrix4x4 lastMatrix;
        private Rect lastCanvas;
        private int frameVersion = -1;
        private Rect geometryRect;
        private bool geometryRound;
        private bool geometryStraight;
        private int geometryVersion = -1;
        private float geometryWidth;

        public override Texture mainTexture => source != null ? source.texture : base.mainTexture;

        /// <summary>Images with this name get a round wood panel (PackPanel's key ring pop-up).</summary>
        public static void RegisterRound(string imageName)
        {
            if (!string.IsNullOrEmpty(imageName))
                roundNames.Add(imageName);
        }

        /// <summary>This image gets a round wood panel.</summary>
        public static void RegisterRound(Image image)
        {
            if (image != null)
            {
                roundImages.RemoveWhere(known => known == null);
                roundImages.Add(image);
            }
        }

        /// <summary>This image gets the calm window wood inside a straight edge (a kit window under the forged-iron frame).</summary>
        public static void RegisterForged(Image image)
        {
            if (image != null)
            {
                forgedImages.RemoveWhere(known => known == null);
                forgedImages.Add(image);
            }
        }

        /// <summary>The wood an image shows: the calm window wood for a forged image (while it is readable), else the wallpaper.</summary>
        private static Sprite WallpaperFor(Image image) =>
            forgedImages.Contains(image) && KitForgedArt.WindowWood != null ? KitForgedArt.WindowWood : KitArt.Wallpaper;

        public static void Apply(Image image, bool on)
        {
            Transform child = image.transform.Find(Name);
            TimberBackground wood = child != null ? child.GetComponent<TimberBackground>() : null;
            if (!on)
            {
                if (wood != null && wood.gameObject.activeSelf)
                {
                    image.fillCenter = wood.originalFill;
                    image.sprite = wood.originalSprite;
                    image.material = wood.originalMaterial;
                    image.type = wood.originalType;
                    image.color = wood.originalColor;
                    wood.gameObject.SetActive(false);
                }
                return;
            }
            if (wood != null && wood.gameObject.activeSelf && image.color == Color.clear
                && wood.source == WallpaperFor(image) && wood.color == PanelTint)
                return;
            if (wood == null)
            {
                GameObject go = new GameObject(Name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TimberBackground));
                go.transform.SetParent(image.transform, false);
                go.transform.SetAsFirstSibling();
                wood = go.GetComponent<TimberBackground>();
                wood.frame = image;
                wood.raycastTarget = false;
                go.SetActive(false);
            }
            if (!wood.gameObject.activeSelf)
            {
                wood.originalFill = image.fillCenter;
                wood.originalSprite = image.sprite;
                wood.originalMaterial = image.material;
                wood.originalType = image.type;
                wood.originalColor = image.color;
            }
            wood.round = roundImages.Contains(image) || roundNames.Contains(image.name);
            wood.straight = !wood.round && forgedImages.Contains(image);
            Transform previousRim = image.transform.Find(KitThemeRules.FrameName);
            if (previousRim != null) previousRim.gameObject.SetActive(false);
            wood.frameVersion = TimberFrame.Version;
            // The original Image remains a raycast surface, but none of its native art is rendered.
            image.material = null;
            image.color = Color.clear;
            image.fillCenter = false;
            // The background artwork and the game's frame are independent textures.
            // In particular, native frame sprites live in an atlas and must not be sampled as wallpaper.
            wood.source = WallpaperFor(image);
            wood.color = PanelTint;
            wood.gameObject.SetActive(true);
            wood.Inset();
            wood.SetAllDirty();
            NightShade.Track(wood);
        }

        private RectTransform Root => canvas != null ? canvas.rootCanvas.transform as RectTransform : null;

        private void Inset()
        {
            if (frame == null)
                return;
            RectTransform rect = rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void LateUpdate()
        {
            TimberFrame.Poll();
            if (frameVersion != TimberFrame.Version)
            {
                frameVersion = TimberFrame.Version;
                Inset();
                SetAllDirty();
            }
            RectTransform root = Root;
            if (root == null || frame == null)
                return;
            Matrix4x4 matrix = root.worldToLocalMatrix * rectTransform.localToWorldMatrix;
            // Moving a panel changes the portion of the wallpaper it reveals, even if its size stays the same.
            if (matrix != lastMatrix || root.rect != lastCanvas)
                SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            RectTransform root = Root;
            if (source == null || root == null)
                return;
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            lastMatrix = root.worldToLocalMatrix * rectTransform.localToWorldMatrix;
            lastCanvas = root.rect;
            Vector4 border = source.border;
            Rect area = source.rect;
            Rect wood = Rect.MinMaxRect(area.xMin + border.x, area.yMin + border.y,
                area.xMax - border.z, area.yMax - border.w);
            Texture texture = mainTexture;
            Vector2 textureSize = new Vector2(texture.width, texture.height);
            WoodPanel(mesh, rect, wood, textureSize);
        }

        // The bevel and fill share exactly the same vertices and wallpaper UVs. There can be no
        // transparent gap between a separate rim and the panel. Only this graphic is clipped.
        private void WoodPanel(VertexHelper mesh, Rect rect, Rect wood, Vector2 textureSize)
        {
            // Opening, fading or moving the panel only changes visibility/UVs. Retain the expensive
            // concave triangulation across those operations, including disable/enable cycles.
            if (geometryVersion != TimberFrame.Version || geometryRect != rect || geometryRound != round || geometryStraight != straight)
            {
                float roughness = straight ? 0f : TimberFrame.Roughness;
                TimberFrame.Contour(rect, round, roughness, contour, normals);
                geometryWidth = TimberFrame.BevelWidth(rect, round, roughness);
                interior.Clear();
                for (int i = 0; i < contour.Count; i++)
                    interior.Add(contour[i] - normals[i] * geometryWidth);
                TimberFrame.Triangulate(interior, triangles);
                geometryRect = rect;
                geometryRound = round;
                geometryStraight = straight;
                geometryVersion = TimberFrame.Version;
            }
            int count = contour.Count;
            float width = geometryWidth;
            for (int ring = 0; ring < TimberGeometry.Rings; ring++)
            {
                float depth = TimberGeometry.RingDepth(ring, width);
                float alpha = TimberGeometry.RingAlpha(ring);
                for (int i = 0; i < count; i++)
                {
                    Vector2 n = normals[i];
                    float shade = TimberGeometry.RingShade(ring, new KitVector(n.x, n.y));
                    Color tint = color * new Color(shade, shade, shade, alpha);
                    Vertex(mesh, contour[i] - n * depth, wood, textureSize, tint);
                }
            }
            for (int ring = 0; ring < TimberGeometry.Rings - 1; ring++)
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    int outer = ring * count, inner = outer + count;
                    mesh.AddTriangle(outer + i, outer + next, inner + next);
                    mesh.AddTriangle(outer + i, inner + next, inner + i);
                }
            for (int i = 0; i < triangles.Count; i += 3)
                mesh.AddTriangle(3 * count + triangles[i], 3 * count + triangles[i + 1], 3 * count + triangles[i + 2]);
        }

        private void Vertex(VertexHelper mesh, Vector2 point, Rect wood, Vector2 textureSize, Color tint)
        {
            Vector2 canvasPoint = lastMatrix.MultiplyPoint3x4(point);
            mesh.AddVert(point, tint, TimberFrame.Uv(canvasPoint, lastCanvas, wood, textureSize));
        }
    }
}

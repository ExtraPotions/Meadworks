// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The plates are MeadWorks' own CC0-built images (Art/source/build_panel_r3.py, build_panel_r4.py, build_panel_r6.py).
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible.UI.PlateColumn;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Slim plates for a compact HUD (Cairn's compass plank and heading inset; the older bronze compass strip and heading plate). Each is a
    /// three-slice (the knotwork ends keep their shape, the middle stretches) drawn at any height by scaling the borders.
    /// </summary>
    public static class KitHudArt
    {
        /// <summary>The compass strip's source size (480 x 56): a dark well with a knot plate at each end and a notch at the top center.</summary>
        public const float CompassStripSourceHeight = 56f;

        /// <summary>The heading plate's source size (170 x 46): the same bronze frame with a smaller knot plate at each end.</summary>
        public const float HeadingPlateSourceHeight = 46f;

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Sprite CompassStrip => Read("compass_strip", 62f);

        public static Sprite HeadingPlate => Read("heading_plate", 42f);

        /// <summary>
        /// The plank images' scale: 4 source pixels per UI unit (the compass plank is 960 x 204 for Cairn's 240 x 51 unit HUD).
        /// Pass <c>height * PlankPixelsPerUnit</c> as <see cref="Plate"/>'s source height so the iron keeps one thickness at any height.
        /// </summary>
        public const float PlankPixelsPerUnit = 4f;

        /// <summary>
        /// Cairn's iron-bound compass plank (option 2, re-picked 2026-10-14): one seamless wood plank held by dark iron straps along the top
        /// and bottom (two rivets each), between iron end caps that stand proud of the straps, with the orange rim and a rivet at the top
        /// and bottom of each cap. Nine-sliced: the caps and straps keep their size. The middle cap rivet is not painted (the middle rows
        /// stretch); place <see cref="IronRivet"/> at <see cref="CapRivetX"/> and half the plank's height.
        /// </summary>
        public static Sprite CompassPlank => Read("compass_plank", new Vector4(120f, 40f, 120f, 40f));

        /// <summary>The center of each end cap from the plank's left and right edges, in UI units; <see cref="CapRivetSize"/> is the rivet image's size.</summary>
        public const float CapRivetX = 11f;
        public const float CapRivetSize = 6.5f;

        /// <summary>The heading notch cut into the plank's lower band: a dark well in an iron rim with the orange line (256 x 56, 3-sliced).</summary>
        public static Sprite CompassInset => Read("compass_inset", new Vector4(24f, 0f, 24f, 0f));

        /// <summary>One domed iron rivet (32 x 32), for the corners of an iron plate.</summary>
        public static Sprite IronRivet => Read("iron_rivet", Vector4.zero);

        /// <summary>
        /// Dresses <paramref name="image"/> in the game's own forged-iron button look (dark textured iron, orange rim), the look Mantle
        /// gives the HUD column squares, so a plate beside them is the same material. Only the game's sprite, material and color are
        /// referenced, never copied. False while the game's inventory screen is not built yet.
        /// </summary>
        public static bool DressAsGameIron(Image image)
        {
            Image? vanilla = InventoryGui.instance != null && InventoryGui.instance.m_takeAllButton != null ? InventoryGui.instance.m_takeAllButton.GetComponent<Image>() : null;
            if (image == null || vanilla == null || vanilla.sprite == null)
                return false;
            image.sprite = vanilla.sprite;
            image.material = vanilla.material;
            image.color = vanilla.color;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return true;
        }

        /// <summary>
        /// A plate image under <paramref name="parent"/>, top-left at <paramref name="at"/>, this size, never catching the pointer.
        /// Null headless or when the image is missing; the product then draws its text without a plate.
        /// </summary>
        public static Image Plate(RectTransform parent, string name, Sprite sprite, float sourceHeight, Vector2 at, Vector2 size)
        {
            if (parent == null || sprite == null)
                return null;
            Transform existing = KitUnity.FindLive(parent, name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            if (existing == null)
                go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            KitAppearance.Fit(image, sourceHeight / Mathf.Max(1f, size.y));
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            return image;
        }

        static Sprite Read(string name, float border) => Read(name, new Vector4(border, 0f, border, 0f));

        internal static Sprite Read(string name, Vector4 border)
        {
            if (sprites.TryGetValue(name, out Sprite sprite))
                return sprite;
            if (!MeadworksHeadless.AllowsUi)
                return null;
            sprites[name] = null;
            string resource = KitArtCatalog.Resource(name);
            if (typeof(KitHudArt).Assembly.GetManifestResourceInfo(resource) == null)
                return null;
            Sprite decoded = EmbeddedSprite.Load(typeof(KitHudArt).Assembly, resource, KitArtCatalog.SpriteName(name));
            if (decoded == null)
                return null;
            Texture2D texture = decoded.texture;
            texture.wrapMode = TextureWrapMode.Clamp;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0u,
                SpriteMeshType.FullRect, border);
            sprite.name = KitArtCatalog.SpriteName(name);
            Object.Destroy(decoded);
            sprites[name] = sprite;
            return sprite;
        }
    }
}

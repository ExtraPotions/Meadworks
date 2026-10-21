using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.PlateColumn;
using ExtraPotions.Meadworks.Crucible.UI;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Look
{
    /// <summary>
    /// Embedded inventory artwork. Brown keeps its original procedural assets; the Timber wallpaper and button come from
    /// the Crucible.UI kit (KitArt), where the art lives once; so do the brown panel, cell, button and ring images. Only the slot icons are Mantle's own resources. Frames are nine-sliced and labels remain live text; slot icons use material-colored art.
    /// Each texture is decoded once with mipmaps; missing Timber images fall back to the original artwork.
    /// </summary>
    public static class SkinArt
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static bool Timber => GridSkin.On && KitArt.Timber;

        public static Sprite Panel => Timber ? Wallpaper : BrownPanel;

        public static Sprite BrownPanel => KitArt.BrownPanel;

        public static Sprite Wallpaper => KitArt.Wallpaper ?? BrownPanel;

        public static Sprite Cell => KitArt.Cell;

        public static Sprite Button => Timber ? KitArt.TimberButton ?? BrownButton : BrownButton;

        private static Sprite BrownButton => KitArt.BrownButton;

        public static Sprite ButtonHover => KitArt.ButtonHover;

        public static Sprite ButtonPressed => KitArt.ButtonPressed;

        /// <summary>The key ring pop-up's round panel and the bronze wire its cells hang on (stretched, no slicing).</summary>
        public static Sprite RingPanel => KitArt.RingPanel;

        public static Sprite RingLine => KitArt.RingLine;

        /// <summary>A slot's item illustration (<c>icon_head.png</c> ...), or null when there is none for that name.</summary>
        public static Sprite Icon(string slot) => Read("icon_" + slot, 100f, 0f);

        /// <summary>The world level globe of the HUD column right of the minimap (Solo's Elite Creatures Reborn square).</summary>
        public static Sprite Globe => Read("globe", 100f, 0f);

        public static bool IsPanel(Sprite sprite) => sprite != null &&
            KitArt.IsPanel(sprite);

        /// <summary>
        /// Every image the panels use, decoded now (at the main menu) rather than the first time the inventory opens:
        /// the Timber wallpaper (1254 px square) and button (1983 x 793) decode with their mipmaps on the main thread,
        /// which stalled the panels' first slide-in (reported 2026-10-04). Decoded images are kept, so later reads are free.
        /// </summary>
        public static void Prewarm()
        {
            Sprite[] all = { Wallpaper, BrownPanel, Cell, Button, BrownButton, ButtonHover, ButtonPressed, RingPanel, RingLine };
            int count = all.Length;
            foreach (Slots.SlotKind kind in System.Enum.GetValues(typeof(Slots.SlotKind)))
            {
                if (kind != Slots.SlotKind.Retired && Icon(kind.ToString().ToLowerInvariant()) != null)
                    count++;
            }
            Plugin.Log.LogInfo($"Mantle: {count} panel images ready");
        }

        private static Sprite Read(string name, float pixelsPerUnit, float border)
        {
            if (sprites.TryGetValue(name, out Sprite sprite))
                return sprite;
            sprites[name] = null;
            string resource = $"Mantle.assets.{name}.png";
            if (typeof(SkinArt).Assembly.GetManifestResourceInfo(resource) == null)
                return null;
            Sprite decoded = EmbeddedSprite.Load(typeof(SkinArt).Assembly, resource, "Mantle_" + name);
            if (decoded == null)
            {
                Plugin.Log.LogWarning($"could not read the embedded image {resource}; the plain brown is used instead");
                return null;
            }
            sprite = Resliced(decoded.texture, name, pixelsPerUnit, border);
            Object.Destroy(decoded);
            sprites[name] = sprite;
            return sprite;
        }

        /// <summary>The decoded texture as a sprite with the given scale and 9-slice border.</summary>
        private static Sprite Resliced(Texture2D texture, string name, float pixelsPerUnit, float border)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            Rect whole = new Rect(0f, 0f, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, whole, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0u, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = "Mantle_" + name;
            return sprite;
        }
    }
}

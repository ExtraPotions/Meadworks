// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/SkinArt.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: renamed SkinArt to KitArt; images embedded once in
// Crucible.UI (resource prefix and sprite names from KitArtCatalog), slicing from Core KitArtCatalog.Slice, theme from
// KitLook, slot icons from the catalogue list, never decoded on a headless process.
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible.UI.PlateColumn;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The kit's embedded artwork. Brown keeps PackPanel's original procedural assets; Timber uses MeadWorks' own CC0-built
    /// wallpaper and button images. Frames are nine-sliced and labels remain live text. Each texture
    /// is decoded once with mipmaps; missing Timber images fall back to the original artwork.
    /// </summary>
    public static class KitArt
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static bool Timber => KitLook.Timber;

        public static Sprite Panel => Timber ? Wallpaper : BrownPanel;

        public static Sprite BrownPanel => Read("panel");

        public static Sprite Wallpaper => Read("timber_background") ?? BrownPanel;

        public static Sprite Cell => Read("cell");

        public static Sprite Button => Timber ? Read("timber_button") ?? BrownButton : BrownButton;

        /// <summary>The Timber button regardless of the kit's own theme (a product with its own theme switch asks for it), or null when it is missing or the process is headless.</summary>
        public static Sprite TimberButton => Read("timber_button");

        public static Sprite BrownButton => Read("button");

        public static Sprite ButtonHover => Read("button_hover");

        public static Sprite ButtonPressed => Read("button_pressed");

        /// <summary>A round pop-up's panel and the bronze wire its cells hang on (stretched, no slicing).</summary>
        public static Sprite RingPanel => Read("ring_panel");

        public static Sprite RingLine => Read("ring_line");

        /// <summary>MeadWorks' own nine-sliced wood border (transparent inside) for the game's large map, drawn thinner round the square minimap.</summary>
        public static Sprite MapFrameLarge => Read("map_frame_large");

        /// <summary>A slot's illustration (<c>icon_head.png</c> ...), or null when there is none for that name.</summary>
        public static Sprite Icon(string slot) => Read("icon_" + slot);

        public static bool IsPanel(Sprite sprite) => sprite != null &&
            (sprite.name == KitArtCatalog.SpriteName("panel") || sprite.name == KitArtCatalog.SpriteName("timber_panel") || TimberFrame.Owns(sprite));

        /// <summary>
        /// Every image the kit uses, decoded now (at the main menu) rather than the first time a panel opens: the Timber
        /// wallpaper (1254 px square) and button (1983 x 793) decode with their mipmaps on the main thread, which stalled
        /// PackPanel's first slide-in (reported 2026-10-04). Decoded images are kept, so later reads are free.
        /// </summary>
        public static int Prewarm()
        {
            Sprite[] all = { Wallpaper, BrownPanel, Cell, Button, BrownButton, ButtonHover, ButtonPressed, RingPanel, RingLine };
            int count = 0;
            foreach (Sprite sprite in all)
                if (sprite != null)
                    count++;
            foreach (string slot in KitArtCatalog.SlotIcons)
                if (Icon(slot) != null)
                    count++;
            CrucibleUiPlugin.Log?.Info($"Crucible.UI: {count} panel images ready");
            return count;
        }

        private static Sprite Read(string name)
        {
            if (sprites.TryGetValue(name, out Sprite sprite))
                return sprite;
            if (!MeadworksHeadless.AllowsUi)
                return null;
            sprites[name] = null;
            string resource = KitArtCatalog.Resource(name);
            if (typeof(KitArt).Assembly.GetManifestResourceInfo(resource) == null)
                return null;
            Sprite decoded = EmbeddedSprite.Load(typeof(KitArt).Assembly, resource, KitArtCatalog.SpriteName(name));
            if (decoded == null)
            {
                CrucibleUiPlugin.Log?.Warning($"could not read the embedded image {resource}; the plain brown is used instead");
                return null;
            }
            sprite = Resliced(decoded.texture, name);
            Object.Destroy(decoded);
            sprites[name] = sprite;
            return sprite;
        }

        /// <summary>The decoded texture as a sprite with the catalogue's scale and 9-slice border.</summary>
        private static Sprite Resliced(Texture2D texture, string name)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            KitSlice slice = KitArtCatalog.Slice(name, texture.width, texture.height);
            Rect whole = new Rect(slice.Area.X, slice.Area.Y, slice.Area.Width, slice.Area.Height);
            float border = slice.Border;
            Sprite sprite = Sprite.Create(texture, whole, new Vector2(0.5f, 0.5f), slice.PixelsPerUnit, 0u, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = KitArtCatalog.SpriteName(name);
            return sprite;
        }
    }
}

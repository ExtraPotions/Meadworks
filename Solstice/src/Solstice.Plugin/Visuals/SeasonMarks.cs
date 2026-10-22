// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The four season marks are drawn once and destroyed on unload. No image file is shipped.
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Calendar;
using ExtraPotions.Solstice.Config;
using ExtraPotions.Solstice.Visuals;
using UnityEngine;

namespace ExtraPotions.Solstice.Runtime
{
    public static class SeasonMarks
    {
        static readonly Texture2D?[] textures = new Texture2D?[Seasons.Count];
        static readonly Sprite?[] sprites = new Sprite?[Seasons.Count];
        static Texture2D? heatTexture;
        static Sprite? heatSprite;

        public static Sprite? For(Season season)
        {
            if (!MeadworksHeadless.AllowsUi) return null;
            var settings = Plugin.Settings;
            if (settings == null || !settings.Get(SolsticeSettings.Visuals.SeasonIcons)) return null;
            return Mark(season);
        }

        /// <summary>The drawn mark. Winter status uses this when Season Icons is off, so the pale game icon is not left on a bright sky.</summary>
        public static Sprite? Mark(Season season)
        {
            if (!MeadworksHeadless.AllowsUi) return null;
            int index = (int)season;
            if ((uint)index >= sprites.Length) return null;
            if (sprites[index] != null) return sprites[index];
            var pixels = SeasonIcon.Draw(season);
            textures[index] = Texture(pixels, "SolsticeSeason" + season);
            sprites[index] = Sprite.Create(textures[index], new Rect(0f, 0f, SeasonIcon.Size, SeasonIcon.Size), new Vector2(0.5f, 0.5f), SeasonIcon.Size);
            return sprites[index];
        }

        /// <summary>The overheat mark. It does not follow the Season Icons setting.</summary>
        public static Sprite? Heat()
        {
            if (!MeadworksHeadless.AllowsUi) return null;
            if (heatSprite != null) return heatSprite;
            heatTexture = Texture(SeasonIcon.DrawHeat(), "SolsticeHeat");
            heatSprite = Sprite.Create(heatTexture, new Rect(0f, 0f, SeasonIcon.Size, SeasonIcon.Size), new Vector2(0.5f, 0.5f), SeasonIcon.Size);
            return heatSprite;
        }

        static Texture2D Texture(byte[] pixels, string name)
        {
            var texture = new Texture2D(SeasonIcon.Size, SeasonIcon.Size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixelData(pixels, 0);
            texture.Apply(false, true);
            return texture;
        }

        public static bool Owns(Sprite? sprite)
        {
            if (sprite == null) return false;
            for (int i = 0; i < sprites.Length; i++)
                if (sprites[i] == sprite) return true;
            return false;
        }

        /// <summary>Drops a mark whose texture or sprite was destroyed with the world. A live pair is kept.</summary>
        public static void ReleaseDestroyed()
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && textures[i] != null) continue;
                if (sprites[i] != null) UnityEngine.Object.Destroy(sprites[i]);
                if (textures[i] != null) UnityEngine.Object.Destroy(textures[i]);
                sprites[i] = null;
                textures[i] = null;
            }
            if (heatSprite != null && heatTexture != null) return;
            if (heatSprite != null) UnityEngine.Object.Destroy(heatSprite);
            if (heatTexture != null) UnityEngine.Object.Destroy(heatTexture);
            heatSprite = null;
            heatTexture = null;
        }

        public static void Shutdown()
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null) UnityEngine.Object.Destroy(sprites[i]);
                if (textures[i] != null) UnityEngine.Object.Destroy(textures[i]);
                sprites[i] = null;
                textures[i] = null;
            }
            if (heatSprite != null) UnityEngine.Object.Destroy(heatSprite);
            if (heatTexture != null) UnityEngine.Object.Destroy(heatTexture);
            heatSprite = null;
            heatTexture = null;
        }
    }
}

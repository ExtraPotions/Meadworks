// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Which of the game's shader properties a material may take, from its shader name.
using System;

namespace ExtraPotions.Solstice.Visuals
{
    /// <summary>Properties Solstice writes. Names are the game's.</summary>
    public static class SeasonShader
    {
        public const string Color = "_Color";
        public const string SnowLevel = "_SnowLevel";
        public const string Shallow = "_ColorBottomShallow";
        public const string Top = "_ColorTop";
        public const string Foam = "_FoamColor";
        public const string Ripple = "_RippleDistance";
    }

    /// <summary>What to do with one material. Flags combine when a material has more than one property.</summary>
    [Flags]
    public enum VisualTarget
    {
        None = 0,
        Grass = 1,
        Leaf = 2,
        Terrain = 4,
        Snow = 8,
        ShallowWater = 16,
        WaterTop = 32,
        WaterFoam = 64,
        WaterRipple = 128,
        WaterSurface = 256
    }

    /// <summary>
    /// Picks grass, leaf, terrain, snow and water from the shader name and which properties exist.
    /// Creature, character, UI, particle and font shaders are left alone. A terrain shader stays terrain even
    /// when its material name says grass. Water does not take <see cref="SeasonShader.SnowLevel"/>.
    /// </summary>
    public static class SeasonVisualSelection
    {
        public static VisualTarget Select(string? shaderName, string? materialName, bool color, bool snowLevel, bool shallow, bool foam = false, bool ripple = false, bool top = false)
        {
            var shader = shaderName ?? "";
            var material = materialName ?? "";
            if (shader.Length == 0 && material.Length == 0) return VisualTarget.None;
            if (Excluded(shader)) return VisualTarget.None;
            var target = VisualTarget.None;
            if (Has(shader, "water"))
            {
                // Custom/Water has no _Color. Writing it does nothing and the frozen look uses the shallow and top colors.
                if (shallow) target |= VisualTarget.ShallowWater;
                if (top) target |= VisualTarget.WaterTop;
                if (foam) target |= VisualTarget.WaterFoam;
                if (ripple) target |= VisualTarget.WaterRipple;
                return target;
            }
            if (snowLevel) target |= VisualTarget.Snow;
            if (!color) return target;
            if (Has(shader, "grass")) target |= VisualTarget.Grass;
            else if (Has(shader, "vegetation") || Has(shader, "foliage") || Has(shader, "leaf")) target |= VisualTarget.Leaf;
            else if (Has(shader, "terrain") || Has(shader, "heightmap")) target |= VisualTarget.Terrain;
            else if (Has(material, "grass")) target |= VisualTarget.Grass;
            else if (Has(material, "leaf") || Has(material, "bush") || Has(material, "shrub") || Has(material, "foliage")) target |= VisualTarget.Leaf;
            return target;
        }

        /// <summary>A bark, trunk, log, or stem material. Shared leaf atlases are not named this way; their brown pixels stay in the frost blend.</summary>
        public static bool BarkOnly(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Has(name, "bark") || Has(name, "trunk") || Has(name, "log") || Has(name, "stem");
        }

        /// <summary>Grass and leaf shaders that can take a frost copy. Terrain, water, and bark-only materials are left alone.</summary>
        public static bool FrostTexture(string? shaderName, string? materialName, out bool leaves) =>
            FrostTexture(shaderName, materialName, null, out leaves);

        /// <summary>
        /// <paramref name="textureName"/> is the source texture. Ground clutter on the grass shader takes the leaf frost.
        /// Meadow grass cards stay on the white frost.
        /// </summary>
        public static bool FrostTexture(string? shaderName, string? materialName, string? textureName, out bool leaves)
        {
            leaves = false;
            var shader = shaderName ?? "";
            var material = materialName ?? "";
            if (shader.Length == 0 && material.Length == 0 && string.IsNullOrEmpty(textureName)) return false;
            if (Excluded(shader)) return false;
            if (Has(shader, "water") || Has(shader, "terrain") || Has(shader, "heightmap")) return false;
            if (BarkOnly(material) || BarkOnly(shader)) return false;
            if (Has(shader, "grass"))
            {
                leaves = GroundClutter(material) || GroundClutter(textureName);
                return true;
            }
            if (Has(shader, "vegetation") || Has(shader, "foliage") || Has(shader, "leaf"))
            {
                leaves = true;
                return true;
            }
            return false;
        }

        /// <summary>Leafy ground cards that share the grass shader: forest groundcover, ferns, shrubs, waterlilies, Black Forest grasscross.</summary>
        public static bool GroundClutter(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Has(name, "groundcover") || Has(name, "ormbunke") || Has(name, "fern")
                || Has(name, "shrub") || Has(name, "bush") || Has(name, "waterlil")
                || Has(name, "clutter") || Has(name, "grasscross_forest") || Has(name, "vass");
        }

        static bool Excluded(string shader) =>
            Has(shader, "creature") || Has(shader, "character") || Has(shader, "player")
            || Has(shader, "ui/") || Has(shader, "gui/") || Has(shader, "textmesh")
            || Has(shader, "particle") || Has(shader, "sprite") || Has(shader, "font")
            || Has(shader, "decal");

        static bool Has(string text, string part) => text.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. Seasonal color is a multiplier on the game's own material color. No texture is replaced.
using ExtraPotions.Solstice.Calendar;

namespace ExtraPotions.Solstice.Visuals
{
    /// <summary>Which color a season changes.</summary>
    public enum TintChannel
    {
        Grass,
        Leaf,
        Terrain,
        Map,
        ShallowWater
    }

    /// <summary>A color as three floats. Values may sit outside 0..1 until <see cref="Rgb.Clamped"/>.</summary>
    public readonly struct Rgb
    {
        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }

        public static float Clamp(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public Rgb Clamped() => new Rgb(Clamp(R), Clamp(G), Clamp(B));
    }

    /// <summary>
    /// Season color math. Grass, leaves, terrain and the map are multiplied, then winter is faded toward gray.
    /// Shallow water moves toward ice only while it is frozen. Snow uses the game's own 0.25 show threshold.
    /// </summary>
    public static class SeasonTint
    {
        /// <summary>WearNTear shows piece snow only once buildup is above this, then remaps the rest to 0..1.</summary>
        public const float SnowShowsFrom = 0.25f;

        /// <summary>How far a frozen shallow color moves toward <see cref="Ice"/>. The water shader has no <c>_Color</c>.</summary>
        public const float IceBlend = 0.55f;

        /// <summary>The water's top color, when the shader has one, moves this far toward ice.</summary>
        public const float SurfaceIceBlend = 0.34f;

        /// <summary>Foam moves toward ice, less than the shallow color, so the surface does not blow out.</summary>
        public const float FoamIceBlend = 0.30f;

        /// <summary>Frozen water spaces <c>_RippleDistance</c> by this factor, and never by more than <see cref="RippleCalmCap"/>.</summary>
        public const float RippleCalm = 1.45f;

        public const float RippleCalmCap = 12f;

        /// <summary>Icy blue, short of white, so a light water color does not become a flat lamp.</summary>
        public static readonly Rgb Ice = new Rgb(0.45f, 0.64f, 0.80f);

        /// <summary>Old multiplier target. Winter ground does not use it. A white <c>_Color</c> only reveals the texture.</summary>
        public static readonly Rgb SnowWhite = new Rgb(0.93f, 0.95f, 0.97f);

        public static Rgb Apply(TintChannel channel, Season season, Rgb color, bool frozen)
        {
            if (channel == TintChannel.ShallowWater) return Freeze(color, frozen);
            var scaled = Multiply(color, Multiplier(channel, season));
            return season == Season.Winter ? Desaturate(scaled, WinterFade(channel)).Clamped() : scaled.Clamped();
        }

        public static Rgb Multiplier(TintChannel channel, Season season)
        {
            switch (channel)
            {
                case TintChannel.Grass: return Grass(season);
                case TintChannel.Leaf: return Leaf(season);
                case TintChannel.Terrain: return Terrain(season);
                case TintChannel.Map: return Map(season);
                default: return new Rgb(1f, 1f, 1f);
            }
        }

        /// <summary>0 keeps the color. 1 is gray at the same brightness.</summary>
        public static float WinterFade(TintChannel channel)
        {
            switch (channel)
            {
                case TintChannel.Grass: return 0.90f;
                case TintChannel.Leaf: return 0.94f;
                case TintChannel.Terrain: return 0.40f;
                case TintChannel.Map: return 0.35f;
                default: return 0f;
            }
        }

        public static Rgb Multiply(Rgb color, Rgb multiplier) =>
            new Rgb(color.R * multiplier.R, color.G * multiplier.G, color.B * multiplier.B);

        public static Rgb Desaturate(Rgb color, float amount)
        {
            if (amount <= 0f) return color;
            if (amount > 1f) amount = 1f;
            float luma = 0.2126f * color.R + 0.7152f * color.G + 0.0722f * color.B;
            float keep = 1f - amount;
            return new Rgb(color.R * keep + luma * amount, color.G * keep + luma * amount, color.B * keep + luma * amount);
        }

        /// <summary>0 is gray. 1 is a single channel.</summary>
        public static float Saturation(Rgb color)
        {
            float max = color.R > color.G ? color.R : color.G;
            if (color.B > max) max = color.B;
            float min = color.R < color.G ? color.R : color.G;
            if (color.B < min) min = color.B;
            return max <= 0f ? 0f : (max - min) / max;
        }

        public static Rgb Freeze(Rgb color, bool frozen) => FreezeAmount(color, frozen, IceBlend);

        public static Rgb FreezeAmount(Rgb color, bool frozen, float blend)
        {
            if (!frozen || blend <= 0f) return color;
            if (blend > 1f) blend = 1f;
            return new Rgb(
                color.R + (Ice.R - color.R) * blend,
                color.G + (Ice.G - color.G) * blend,
                color.B + (Ice.B - color.B) * blend).Clamped();
        }

        /// <summary>A larger ripple distance spaces the waves. Negative and non-finite values are left alone.</summary>
        public static float CalmRipple(float original, bool frozen)
        {
            if (!frozen || float.IsNaN(original) || float.IsInfinity(original) || original < 0f) return original;
            float next = original * RippleCalm;
            float cap = original + RippleCalmCap;
            return next > cap ? cap : next;
        }

        /// <summary>Blends <paramref name="color"/> toward <see cref="SnowWhite"/> by <see cref="SnowLevel"/>.</summary>
        public static Rgb Whiten(Rgb color, float cover)
        {
            float level = SnowLevel(cover);
            if (level <= 0f) return color;
            return new Rgb(
                color.R + (SnowWhite.R - color.R) * level,
                color.G + (SnowWhite.G - color.G) * level,
                color.B + (SnowWhite.B - color.B) * level).Clamped();
        }

        /// <summary>The shader value for <c>_SnowLevel</c>. At or below <see cref="SnowShowsFrom"/> it is 0.</summary>
        public static float SnowLevel(float cover)
        {
            if (float.IsNaN(cover) || cover <= SnowShowsFrom) return 0f;
            float level = (cover - SnowShowsFrom) / (1f - SnowShowsFrom);
            if (level < 0f) return 0f;
            return level > 1f ? 1f : level;
        }

        /// <summary>Never lowers a material that is already snowier than this season.</summary>
        public static float SnowApply(float original, float cover)
        {
            if (float.IsNaN(original)) original = 0f;
            float wanted = SnowLevel(cover);
            return original > wanted ? original : wanted;
        }

        static Rgb Grass(Season season)
        {
            switch (season)
            {
                case Season.Spring: return new Rgb(0.90f, 1.12f, 0.86f);
                case Season.Summer: return new Rgb(0.82f, 1.00f, 0.70f);
                case Season.Fall: return new Rgb(1.15f, 0.92f, 0.58f);
                default: return new Rgb(0.70f, 0.74f, 1.08f);
            }
        }

        static Rgb Leaf(Season season)
        {
            switch (season)
            {
                case Season.Spring: return new Rgb(0.86f, 1.16f, 0.78f);
                case Season.Summer: return new Rgb(0.80f, 1.02f, 0.68f);
                case Season.Fall: return new Rgb(1.40f, 0.58f, 0.26f);
                default: return new Rgb(0.72f, 0.76f, 1.06f);
            }
        }

        static Rgb Terrain(Season season)
        {
            switch (season)
            {
                case Season.Spring: return new Rgb(0.97f, 1.05f, 0.95f);
                case Season.Summer: return new Rgb(0.95f, 1.02f, 0.90f);
                case Season.Fall: return new Rgb(1.08f, 0.97f, 0.84f);
                default: return new Rgb(0.96f, 0.98f, 1.02f);
            }
        }

        static Rgb Map(Season season)
        {
            switch (season)
            {
                case Season.Spring: return new Rgb(0.93f, 1.05f, 0.92f);
                case Season.Summer: return new Rgb(0.90f, 1.02f, 0.86f);
                case Season.Fall: return new Rgb(1.10f, 0.95f, 0.80f);
                default: return new Rgb(0.88f, 0.92f, 1.00f);
            }
        }
    }
}

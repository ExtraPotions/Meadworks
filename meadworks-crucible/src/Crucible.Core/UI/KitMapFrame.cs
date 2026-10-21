// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks Map Frames: MeadWorks' own frame images drawn as overlays on the game's minimap and large map. Map contents (terrain, pins, fog)
// and the game's textures are never replaced or exported; switching the option off removes the overlays and leaves the game untouched.
using System;

namespace ExtraPotions.Meadworks.Crucible
{
    public enum KitMapFrameKind { Small, Large }

    public enum KitMapFrameAction { None, Build, Keep, Remove }

    public static class KitMapFrameRules
    {
        /// <summary>The config entry's name (section Look of Crucible.UI's config).</summary>
        public const string ConfigName = "Meadworks Map Frames";

        /// <summary>User decision 2026-10-12 18:55 (Solo draws no map frames): off by default; the option stays available.</summary>
        public const bool DefaultOn = false;

        /// <summary>The large map frame's wood, in UI units: map_frame_large's 56 px border at 200 px per unit over the canvas reference of 100.</summary>
        public const float LargeBorder = 28f;

        /// <summary>The minimap frame's wood, in UI units: the same sliced art drawn thin for the game's square minimap of about 190 units.</summary>
        public const float SmallBorder = 8f;

        /// <summary>The wood's thickness in UI units for this kind.</summary>
        public static float Thickness(KitMapFrameKind kind) => kind == KitMapFrameKind.Large ? LargeBorder : SmallBorder;

        /// <summary>The Image's pixelsPerUnitMultiplier that draws map_frame_large's slice at <see cref="Thickness"/>: 1 for the large map, larger (thinner) for the minimap.</summary>
        public static float PixelsPerUnitMultiplier(KitMapFrameKind kind) => LargeBorder / Thickness(kind);

        /// <summary>
        /// The overlay's size for a map rect of this size: the map rect plus the wood on every side, so the frame sits outside the map and
        /// covers none of it (on the large map, the biome name at its top edge stays readable).
        /// </summary>
        public static KitRect Overlay(KitMapFrameKind kind, float mapWidth, float mapHeight)
        {
            var pad = 2f * Thickness(kind);
            return new KitRect(0f, 0f, Math.Max(0f, mapWidth) + pad, Math.Max(0f, mapHeight) + pad);
        }

        /// <summary>What to do this check: build once while on, keep it, remove it when off or when the map cannot take one; nothing when there is nothing to do.</summary>
        public static KitMapFrameAction Decide(bool enabled, bool canBuild, bool exists)
        {
            if (enabled && canBuild) return exists ? KitMapFrameAction.Keep : KitMapFrameAction.Build;
            return exists ? KitMapFrameAction.Remove : KitMapFrameAction.None;
        }
    }
}

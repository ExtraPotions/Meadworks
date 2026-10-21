// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The colour of a magic item's backdrop: its rarity's hue at full value and less saturation (the full colours,
    /// #1EFF00 and #0070DD, glow like neon on the wood; judged on the 2026-10-05 mockups), opaque, since the sprite
    /// carries the alpha. One tone per rarity definition, worked out once and kept on it. Normal, plain and unknown-rarity items have none.
    /// </summary>
    internal static class BackdropTone
    {
        private const float Saturation = 0.6f;

        /// <summary>The item's backdrop tone; false when it shows none. Plain items return at the custom-data check.</summary>
        public static bool TryGet(ItemDrop.ItemData? item, out Color tone)
        {
            tone = default;
            if (item == null || item.m_customData == null || item.m_customData.Count == 0)
            {
                return false;
            }
            RarityDef? rarity = ItemState.Read(item).Rarity;
            if (rarity == null || rarity.IsBase)
            {
                return false;
            }
            Color? kept = rarity.BackdropTone;
            if (kept == null)
            {
                kept = Tone(rarity.Color32);
                rarity.BackdropTone = kept;
            }
            tone = kept.Value;
            return true;
        }

        private static Color Tone(Color full)
        {
            Color.RGBToHSV(full, out float hue, out float saturation, out _);
            return Color.HSVToRGB(hue, saturation * Saturation, 1f);
        }
    }
}

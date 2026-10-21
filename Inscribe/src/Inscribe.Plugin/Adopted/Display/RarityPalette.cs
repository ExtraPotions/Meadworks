// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Rules;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The rich-text color tags of the rarity palette (display.md section 2), built once per rules apply and never per
    /// draw. The palette itself is synced YAML (every player agrees what "orange" means); this is only its text form.
    /// Runs on every peer; nothing here is sent anywhere.
    /// </summary>
    internal static class RarityPalette
    {
        /// <summary>Grey for dormant, unknown and secondary lines.</summary>
        public const string Grey = "<color=#808080>";

        /// <summary>Dark red for the sealed marker (DSP-5).</summary>
        public const string SealedRed = "<color=#B22222>";

        public const string Close = "</color>";

        private static Dictionary<string, string> _openTags = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Rebuilds the tags from the running rules. Plugin Awake and every <c>RulesChanged</c>.</summary>
        public static void Rebuild()
        {
            Dictionary<string, string> tags = new Dictionary<string, string>(StringComparer.Ordinal);
            IReadOnlyList<RarityDef> rarities = ActiveRules.Current.Economy.Rarities;
            for (int i = 0; i < rarities.Count; i++)
            {
                tags[rarities[i].Id] = OpenTag(rarities[i].Color32);
            }
            _openTags = tags;
        }

        /// <summary>
        /// The opening color tag for an item's name, or null when the name stays vanilla: Common, non-magic and unknown
        /// rarities draw in the default text color (item-data.md section 6).
        /// </summary>
        public static string? NameTag(RarityDef? rarity)
        {
            if (rarity == null || rarity.IsBase)
            {
                return null;
            }
            return _openTags.TryGetValue(rarity.Id, out string tag) ? tag : null;
        }

        /// <summary>
        /// The name color as a vertex color, for labels colored through their <c>color</c> property rather than a
        /// rich-text tag (the crafting panel's labels, which the game rewrites every frame). False when the name stays
        /// vanilla: <c>Colored item names</c> off, Common, non-magic or unknown rarity. Allocation-free.
        /// </summary>
        public static bool TryNameColor(RarityDef? rarity, out Color color)
        {
            color = Color.white;
            if (!Config.ModSettings.ColoredItemNames.Value || NameTag(rarity) == null)
            {
                return false;
            }
            color = rarity!.Color32;
            return true;
        }

        /// <summary>The opening tag of any rarity (for the tooltip's rarity line), white when unknown.</summary>
        public static string Tag(RarityDef rarity) => _openTags.TryGetValue(rarity.Id, out string tag) ? tag : "<color=#FFFFFF>";

        /// <summary><c>&lt;color=#RRGGBB&gt;</c> for a color.</summary>
        public static string OpenTag(Color32 color) => "<color=#" + Hex(color) + ">";

        public static string Hex(Color32 color) => color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");
    }
}

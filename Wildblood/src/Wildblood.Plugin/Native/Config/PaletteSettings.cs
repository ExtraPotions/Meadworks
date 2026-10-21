// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using BepInEx.Configuration;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Util;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Config
{
    /// <summary>
    /// The per-player star colour palette. These are local settings: they change only what this player sees, so the
    /// server never binds them. Colour is always redundant with the name, so editing it loses no information.
    /// </summary>
    public static class PaletteSettings
    {
        public const string Section = "9 - Palette (per player)";

        private static readonly NativeEntry<string>[] Colors = new NativeEntry<string>[MutationCatalog.InOrder.Length];

        /// <summary>Counts edits to any colour, so text drawn with the palette and kept knows when to draw again.</summary>
        public static int Version { get; private set; }

        public static void Bind(NativeConfiguration config)
        {
            foreach (Mutation mutation in MutationCatalog.InOrder)
            {
                string key = $"{MutationCatalog.Word(mutation)} star color";
                Colors[(int)mutation] = config.Bind(Section, key, MutationCatalog.DefaultColorHex(mutation),
                    $"Color of a star carrying the {MutationCatalog.Word(mutation)} mutation. Client side.");
                Colors[(int)mutation].SettingChanged += (_, _) => Version++;
            }
        }

        public static Color Of(Mutation mutation)
        {
            NativeEntry<string> entry = Colors[(int)mutation];
            Color fallback = ColorParse.Hex(MutationCatalog.DefaultColorHex(mutation), Color.white);
            return ColorParse.Hex(entry.Value, fallback);
        }
    }
}

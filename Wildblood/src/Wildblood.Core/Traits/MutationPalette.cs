// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later, adapted from
// EliteCreaturesReborn/Traits/MutationCatalog.cs (default star colours) at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: one Local text setting (design 4.10 Palette) instead of nineteen entries; parser and canonical form.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Wildblood.Traits
{
    /// <summary>
    /// The star colour of each mutation, stored as one text <c>Mad=#E23030, Bloated=#8B5A2B, ...</c>. Unknown names and
    /// unreadable colours are dropped; a missing mutation takes its EliteCreaturesReborn colour.
    /// </summary>
    public static class MutationPalette
    {
        private static readonly IReadOnlyDictionary<Mutation, string> Defaults = new Dictionary<Mutation, string>
        {
            [Mutation.Mad] = "#E23030",
            [Mutation.Bloated] = "#8B5A2B",
            [Mutation.Cloaked] = "#4AA6FF",
            [Mutation.Splintering] = "#FFFFFF",
            [Mutation.Leeching] = "#33CC33",
            [Mutation.Warding] = "#101C50",
            [Mutation.Plated] = "#F2C40C",
            [Mutation.Miasmic] = "#1E6B2E",
            [Mutation.Devouring] = "#7A0F0F",
            [Mutation.Thieving] = "#A64BE0",
            [Mutation.Gilded] = "#FFE066",
            [Mutation.Blinking] = "#29E0E0",
            [Mutation.Relentless] = "#FF7F24",
            [Mutation.Juggernaut] = "#6E6E6E",
            [Mutation.Screecher] = "#FF5CC8",
            [Mutation.Frostbound] = "#BFEFFF",
            [Mutation.Mudbound] = "#5E4A1E",
            [Mutation.Corrodent] = "#B7410E",
            [Mutation.Cloning] = "#B4A8FF",
            [Mutation.Flamebound] = "#FF4A1C",
            [Mutation.Piercing] = "#A8B8C8",
            [Mutation.Howling] = "#D4C08A",
            [Mutation.Binding] = "#6B8E23",
        };

        /// <summary>The canonical text of the default palette.</summary>
        public static string Default { get; } = Format(Defaults);

        public static string DefaultColour(Mutation mutation) => Defaults[mutation];

        /// <summary>Every mutation's colour from a stored text.</summary>
        public static IReadOnlyDictionary<Mutation, string> Parse(string? text)
        {
            var result = new Dictionary<Mutation, string>(Defaults);
            foreach (var part in (text ?? "").Split(','))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0) continue;
                var name = part.Substring(0, eq).Trim();
                var colour = part.Substring(eq + 1).Trim();
                if (!Enum.TryParse(name, true, out Mutation mutation) || !Enum.IsDefined(typeof(Mutation), mutation)) continue;
                if (TryColour(colour, out var canonical)) result[mutation] = canonical;
            }
            return result;
        }

        public static string Normalize(string? text) => Format(Parse(text));

        /// <summary><c>#RRGGBB</c>, any case, gives the uppercase form.</summary>
        public static bool TryColour(string? text, out string canonical)
        {
            canonical = "";
            var t = (text ?? "").Trim();
            if (t.Length != 7 || t[0] != '#') return false;
            if (!int.TryParse(t.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return false;
            canonical = t.ToUpperInvariant();
            return true;
        }

        private static string Format(IReadOnlyDictionary<Mutation, string> colours) =>
            string.Join(", ", Mutations.InOrder.Select(m => m + "=" + colours[m]));
    }
}

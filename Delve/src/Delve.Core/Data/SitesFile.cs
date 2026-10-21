// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Delve.Sites.yml (design 4.5): per dungeon generator, the entrance radius (a player this close to
// the entrance is told and holds the site) and the inside radius (around the generator, up in the interior).
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Delve.Data
{
    public sealed class SitesFile
    {
        public const string FileName = "Delve.Sites.yml";
        public const float MaxRadius = 2000f;

        private SitesFile(IReadOnlyDictionary<string, (float, float)> radii, IReadOnlyList<string> errors)
        {
            Radii = radii;
            Errors = errors;
        }

        public static SitesFile Empty { get; } = new SitesFile(new Dictionary<string, (float, float)>(), Array.Empty<string>());

        public IReadOnlyDictionary<string, (float Entrance, float Inside)> Radii { get; }
        public IReadOnlyList<string> Errors { get; }

        /// <summary>The generator's radii, or the defaults given (the kind catalog's).</summary>
        public (float Entrance, float Inside) RadiiOf(string generator, float entrance, float inside) =>
            generator != null && Radii.TryGetValue(generator, out var r) ? r : (entrance, inside);

        public static SitesFile Parse(string? text)
        {
            var errors = new List<string>();
            var radii = new Dictionary<string, (float, float)>(StringComparer.Ordinal);
            var sections = MiniYaml.Read(text, errors);
            if (sections.TryGetValue("sites", out var entries))
                foreach (var e in entries)
                {
                    if (e.Map != null && Radius(e.Map, "entrance", out var entrance) && Radius(e.Map, "inside", out var inside))
                        radii[e.Name] = (entrance, inside);
                    else
                        errors.Add("line " + e.Line + ": '" + e.Name + "' needs {entrance: meters, inside: meters} from 0 to " + MaxRadius + "; the defaults stay");
                }
            return new SitesFile(radii, errors);
        }

        private static bool Radius(IReadOnlyDictionary<string, string> map, string key, out float value)
        {
            value = 0f;
            return map.TryGetValue(key, out var text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && value >= 0f && value <= MaxRadius;
        }
    }
}

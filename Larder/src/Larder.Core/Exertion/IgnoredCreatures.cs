// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Original name matching for the public Safe Stamina behavior; no reference implementation copied.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Larder.Exertion
{
    public sealed class IgnoredCreatures
    {
        readonly HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IgnoredCreatures(string specification)
        {
            foreach (string part in (specification ?? "").Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0) names.Add(name);
            }
        }

        public bool Contains(string prefab, string displayedName) =>
            (!string.IsNullOrEmpty(prefab) && names.Contains(prefab)) ||
            (!string.IsNullOrEmpty(displayedName) && names.Contains(displayedName));
    }
}

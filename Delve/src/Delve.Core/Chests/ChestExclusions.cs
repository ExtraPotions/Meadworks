// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Delve.Chests
{
    public sealed class ChestExclusions
    {
        readonly HashSet<string> excluded = new HashSet<string>(StringComparer.Ordinal);
        string? current;
        public int Count => excluded.Count;

        public bool Update(string value)
        {
            if (string.Equals(current, value, StringComparison.Ordinal)) return false;
            excluded.Clear();
            foreach (string name in value.Split(','))
                if (name.Trim().Length > 0) excluded.Add(name.Trim());
            current = value;
            return true;
        }

        public bool Allows(string prefab) => !excluded.Contains(prefab);
        public void Clear() { excluded.Clear(); current = null; }
    }
}

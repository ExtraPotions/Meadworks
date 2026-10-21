// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ExtraPotions.Hex;

// Numeric toggle keys restore a value actually selected this session; no cheat value is guessed.
public sealed class HexHotkeyValues
{
    readonly Dictionary<string, object> selected = new(StringComparer.Ordinal);
    public void Remember(string key, object current, object safe)
    { if (current is int && !Equals(current, safe)) selected[key] = current; }
    public bool Next(string key, object current, object safe, out object next)
    {
        Remember(key, current, safe);
        next = safe;
        if (current is bool flag) { next = !flag; return true; }
        if (current is not int) return false;
        if (!Equals(current, safe)) return true;
        return selected.TryGetValue(key, out next!);
    }
}

// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.AdoptedCm;

internal static partial class CreatureModifierManager
{
    /// <summary>The keys of the modifiers stored on <paramref name="character"/>, in catalog order; empty without network state.
    /// Read-only: `wildblood spawn` reports which forced modifiers stayed after the trait-overlap rule.</summary>
    internal static List<string> StoredModifierKeys(Character character)
    {
        List<string> keys = new List<string>();
        if (!TryGetZdo(character, out ZDO zdo)) return keys;
        ModifierMask mask = GetStoredModifierMask(zdo);
        foreach (ModifierSpec spec in ModifierSpecs)
            if ((spec.Mask & mask) != 0) keys.Add(spec.Key);
        return keys;
    }
}

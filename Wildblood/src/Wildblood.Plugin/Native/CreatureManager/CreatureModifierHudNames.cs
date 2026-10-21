// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Wildblood.Localization;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Wildblood.AdoptedCm;

/// <summary>Read-only access for the trait line above a nameplate: which native modifiers the HUD shows, their names, and
/// the HUD font the resistance text also copies.</summary>
internal static partial class CreatureModifierManager
{
    /// <summary>The modifiers the nameplate's icon row shows now (the same cached state the row is drawn from); 0 for none.</summary>
    internal static long HudModifierMask(Character character, GameObject hudGui) =>
        TryGetHudModifierState(character, hudGui, out ModifierMask visible, out _, out _, out _, out _) ? (long)visible : 0L;

    private static readonly HashSet<string> CatalogKeys = new HashSet<string>(WildbloodTextKeys.All.Select(k => k.Key), StringComparer.Ordinal);

    /// <summary>Adds the display name of every modifier in <paramref name="mask"/>, in catalog order.</summary>
    internal static void AddHudModifierNames(long mask, List<string> names)
    {
        if (mask == 0) return;
        foreach (ModifierSpec spec in ModifierSpecs)
            if (((long)spec.Mask & mask) != 0) names.Add(HudModifierName(spec.Key));
    }

    // Named as the lore window names it: Wildblood's own modifier_<id> text, else the Creature Manager token read the lore
    // way, which falls back to the English name instead of the game's "[cm_modifier_..._name]" for a missing token.
    private static string HudModifierName(string key)
    {
        string id = string.Equals(key, "juggernaut", StringComparison.OrdinalIgnoreCase) ? "crushing" : key.ToLowerInvariant();
        return CatalogKeys.Contains("modifier_" + id)
            ? ExtraPotions.Wildblood.Runtime.WildbloodText.Text("modifier_" + id)
            : CreatureLocalization.ReadLore(() => GetModifierDisplayName(key));
    }

    /// <summary>Gives <paramref name="target"/> the name text's font and material, as the resistance text does.</summary>
    internal static void ApplyNameFont(TMP_Text target, TMP_Text? nameText) => ApplyHudFont(target, nameText);
}

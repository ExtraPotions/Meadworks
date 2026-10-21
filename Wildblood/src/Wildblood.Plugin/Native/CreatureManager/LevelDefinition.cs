// From CreatureManager 1.2.5 (sighsorry1029), pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// GPL-3.0-only; original LICENSE retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Wildblood modifier selection only, no CM star/stat writer.
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
namespace ExtraPotions.Wildblood.AdoptedCm;

internal sealed class LevelDefinition
{
    private Dictionary<string, ModifierDefinition>? _modifiers;

    public string? Target { get; set; }
    public string? Biome { get; set; }
    public List<string>? Prefabs { get; set; }
    public List<float>? Level { get; set; }
    public float? Damage { get; set; }
    public float? DamagePerLevel { get; set; }
    public float? Health { get; set; }
    public float? HealthPerLevel { get; set; }
    public float? ScalePerLevel { get; set; }
    public List<float>? DistanceScaling { get; set; }
    public List<float>? ModifierDistanceScaling { get; set; }
    public Dictionary<string, ModifierDefinition>? Modifiers
    {
        get => _modifiers;
        set => _modifiers = value == null
            ? null
            : new Dictionary<string, ModifierDefinition>(value, StringComparer.OrdinalIgnoreCase);
    }

    public bool ModifiersCleared { get; set; }
    public bool IsPreset { get; set; }
}

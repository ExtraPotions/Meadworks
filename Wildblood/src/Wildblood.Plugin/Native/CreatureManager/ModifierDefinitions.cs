// From CreatureManager 1.2.5 (sighsorry1029), CreatureDefinitions.cs, pinned at 47e45a6e8c3b389a51e42fc7387d1a4fb90af473.
// Licensed under GPL-3.0-only; original LICENSE is retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: isolated Wildblood source closure; see docs/cm-native-source-map.json.
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.AdoptedCm;

internal static class CreatureModifierCatalog
{
    internal static readonly IReadOnlyList<string> Keys = Array.AsReadOnly(new[]
    {
        "enraged", "fire", "frost", "lightning", "spirit", "armorPiercing", "staggering", "undodgeable",
        "armored", "deathward", "regenerating", "reflection", "vortex", "adaptive", "unflinching", "chameleon",
        "exposed", "weakened", "withered", "crippling", "disruptive", "adrenalineDrain", "corrosive", "toxicDeath",
        "swift", "attackSpeed", "vampiric", "reaping", "blink", "omen", "juggernaut", "blamer"
    });

    private static readonly HashSet<string> KeySet = new(Keys, StringComparer.OrdinalIgnoreCase);

    internal static bool IsKnown(string modifier)
    {
        return !string.IsNullOrWhiteSpace(modifier) && KeySet.Contains(modifier.Trim());
    }
}

internal sealed class ModifierDefinition
{
    public float? Chance { get; set; }
    public float? Power { get; set; }
    public float? Cooldown { get; set; }
    public int? MaxActivations { get; set; }
    public float? MaxRange { get; set; }
    public string? StartEffect { get; set; }
    public float? ProcChance { get; set; }
    public float? Duration { get; set; }
    public float? SecondaryPower { get; set; }
    public float? Radius { get; set; }
    public string? TriggerEffect { get; set; }
    public float? MaxKarmaGain { get; set; }
    public float? FleeHealthRatio { get; set; }
    public float? RegeneratingHealthPerSecondCap { get; set; }
    public int? ReapingHealMaxActivations { get; set; }
    public float? ReapingMaxHealthPerKill { get; set; }
    public float? ReapingMaxHealthCap { get; set; }
    public float? ReapingDamagePerKill { get; set; }
    public float? ReapingDamageCap { get; set; }
    public float? ReapingScalePerKill { get; set; }
    public float? ReapingScaleCap { get; set; }

    internal ModifierDefinition Clone()
    {
        ModifierDefinition clone = new();
        clone.OverlayFrom(this);
        return clone;
    }

    internal void OverlayFrom(ModifierDefinition source)
    {
        if (source.Chance.HasValue) Chance = source.Chance;
        if (source.Power.HasValue) Power = source.Power;
        if (source.Cooldown.HasValue) Cooldown = source.Cooldown;
        if (source.MaxActivations.HasValue) MaxActivations = source.MaxActivations;
        if (source.MaxRange.HasValue) MaxRange = source.MaxRange;
        if (source.StartEffect != null) StartEffect = source.StartEffect;
        if (source.ProcChance.HasValue) ProcChance = source.ProcChance;
        if (source.Duration.HasValue) Duration = source.Duration;
        if (source.SecondaryPower.HasValue) SecondaryPower = source.SecondaryPower;
        if (source.Radius.HasValue) Radius = source.Radius;
        if (source.TriggerEffect != null) TriggerEffect = source.TriggerEffect;
        if (source.MaxKarmaGain.HasValue) MaxKarmaGain = source.MaxKarmaGain;
        if (source.FleeHealthRatio.HasValue) FleeHealthRatio = source.FleeHealthRatio;
        if (source.RegeneratingHealthPerSecondCap.HasValue)
            RegeneratingHealthPerSecondCap = source.RegeneratingHealthPerSecondCap;
        if (source.ReapingHealMaxActivations.HasValue) ReapingHealMaxActivations = source.ReapingHealMaxActivations;
        if (source.ReapingMaxHealthPerKill.HasValue) ReapingMaxHealthPerKill = source.ReapingMaxHealthPerKill;
        if (source.ReapingMaxHealthCap.HasValue) ReapingMaxHealthCap = source.ReapingMaxHealthCap;
        if (source.ReapingDamagePerKill.HasValue) ReapingDamagePerKill = source.ReapingDamagePerKill;
        if (source.ReapingDamageCap.HasValue) ReapingDamageCap = source.ReapingDamageCap;
        if (source.ReapingScalePerKill.HasValue) ReapingScalePerKill = source.ReapingScalePerKill;
        if (source.ReapingScaleCap.HasValue) ReapingScaleCap = source.ReapingScaleCap;
    }
}

internal sealed class ModifierChanceDefinition
{
    private readonly Dictionary<string, float> _values = new(StringComparer.OrdinalIgnoreCase);

    internal float? Get(string modifier)
    {
        return _values.TryGetValue(modifier, out float chance) ? chance : null;
    }

    internal void Set(string modifier, float chance)
    {
        _values[modifier] = chance;
    }
}

internal sealed class ModifierPowerDefinition
{
    private readonly Dictionary<string, float> _values = new(StringComparer.OrdinalIgnoreCase);

    internal float? Get(string modifier)
    {
        return _values.TryGetValue(modifier, out float power) ? power : null;
    }

    internal void Set(string modifier, float power)
    {
        _values[modifier] = power;
    }

    public float? DeathwardCooldown { get; set; }
    public int? DeathwardMaxActivations { get; set; }
    public float? RegeneratingHealthPerSecondCap { get; set; }
    public float? ReflectionProcChance { get; set; }

    public float? ExposedProcChance { get; set; }
    public float? ExposedDuration { get; set; }
    public float? WeakenedProcChance { get; set; }
    public float? WeakenedDuration { get; set; }
    public float? WitheredProcChance { get; set; }
    public float? WitheredDuration { get; set; }
    public float? CripplingJump { get; set; }
    public float? CripplingProcChance { get; set; }
    public float? CripplingDuration { get; set; }
    public float? DisruptiveEitr { get; set; }
    public float? DisruptiveProcChance { get; set; }
    public float? DisruptiveDuration { get; set; }
    public float? AdrenalineDrainGainReduction { get; set; }
    public float? AdrenalineDrainProcChance { get; set; }
    public float? AdrenalineDrainDuration { get; set; }
    public float? CorrosiveProcChance { get; set; }
    public float? CorrosiveDuration { get; set; }
    public float? ToxicDeathRadius { get; set; }
    public string? ToxicDeathTriggerEffect { get; set; }

    public int? ReapingHealMaxActivations { get; set; }
    public float? ReapingMaxHealthPerKill { get; set; }
    public float? ReapingMaxHealthCap { get; set; }
    public float? ReapingDamagePerKill { get; set; }
    public float? ReapingDamageCap { get; set; }
    public float? ReapingScalePerKill { get; set; }
    public float? ReapingScaleCap { get; set; }
    public float? BlinkCooldown { get; set; }
    public float? BlinkMaxRange { get; set; }
    public string? BlinkStartEffect { get; set; }
    public float? KnockbackCooldown { get; set; }
    public float? BlamerMaxKarmaGain { get; set; }
    public float? BlamerFleeHealthRatio { get; set; }
}

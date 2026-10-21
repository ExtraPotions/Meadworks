// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8:
// SecondaryAttackMagicSummonNormalizer.cs. Source copyright and licence retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: Edge namespace, catalog and diagnostics seams; Unity rotation value replaced by a portable
// RotationOffset; world-applied snapshot omitted; generated clone names use Edge. Normalisation rules retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Edge.Secondaries.Adopted;

internal static class SecondaryAttackMagicSummonNormalizer
{
    private const int MaxSpawnChoiceWeight = 100;

    internal static Dictionary<string, NormalizedMagicSummonOverrideConfig> Normalize(
        IReadOnlyDictionary<string, BloodMagicWeaponConfig> bloodMagic)
    {
        Dictionary<string, NormalizedMagicSummonOverrideConfig> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string entryId, BloodMagicWeaponConfig weaponConfig) in bloodMagic)
        {
            if (string.IsNullOrWhiteSpace(entryId) ||
                entryId.Equals("Global", StringComparison.OrdinalIgnoreCase) ||
                weaponConfig?.Enabled == false ||
                weaponConfig?.Summon == null)
            {
                continue;
            }

            MagicSummonOverrideConfig raw = weaponConfig.Summon;
            string normalizedEntryId = entryId.Trim();
            MagicSummonQualityPreset qualityPreset = NormalizeMagicSummonQualityPreset(raw.QualityPreset, normalizedEntryId);
            int? lifetimeSeconds = NormalizeLifetimeSeconds(raw.LifetimeSeconds, normalizedEntryId);

            List<MagicSummonCloneConfig> rawSpawnChoices = raw.SpawnChoices?
                .Where(summon => summon != null)
                .ToList()
                ?? new List<MagicSummonCloneConfig>();

            List<NormalizedMagicSummonCloneConfig> normalizedSummons = new();
            HashSet<string> clonePrefabNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (MagicSummonCloneConfig rawSummon in rawSpawnChoices)
            {
                int weight = ClampInt(rawSummon.Weight ?? 1, 0, MaxSpawnChoiceWeight);
                if (weight <= 0)
                {
                    continue;
                }

                string sourcePrefab = TrimOrEmpty(rawSummon.SourcePrefab);
                if (string.IsNullOrWhiteSpace(sourcePrefab))
                {
                    RuleDiagnostics.Warning(
                        $"Skipping summon override '{normalizedEntryId}' from {SecondaryAttackYamlDomainRegistry.BloodMagicYamlFileName}: sourcePrefab is required.");
                    continue;
                }

                string clonePrefab = TrimOrEmpty(rawSummon.ClonePrefab);
                if (string.IsNullOrWhiteSpace(clonePrefab))
                {
                    clonePrefab = $"Edge_{SanitizeMagicSummonId(normalizedEntryId)}_{SanitizeMagicSummonId(sourcePrefab)}";
                }

                if (!clonePrefabNames.Add(clonePrefab))
                {
                    RuleDiagnostics.Warning(
                        $"Skipping duplicate clonePrefab '{clonePrefab}' in summon override '{normalizedEntryId}'.");
                    continue;
                }

                normalizedSummons.Add(new NormalizedMagicSummonCloneConfig
                {
                    SourcePrefab = sourcePrefab,
                    ClonePrefab = clonePrefab,
                    DisplayName = TrimOrEmpty(rawSummon.DisplayName),
                    Health = rawSummon.Health,
                    Weight = weight
                });
            }

            if (normalizedSummons.Count == 0)
            {
                if (qualityPreset == MagicSummonQualityPreset.None && !lifetimeSeconds.HasValue)
                {
                    continue;
                }
            }

            normalized[normalizedEntryId] = new NormalizedMagicSummonOverrideConfig
            {
                EntryId = normalizedEntryId,
                QualityPreset = qualityPreset,
                LifetimeSeconds = lifetimeSeconds,
                Summons = normalizedSummons
            };
        }

        return normalized;
    }

    private static string TrimOrEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim();
    }

    private static MagicSummonQualityPreset NormalizeMagicSummonQualityPreset(string? rawPreset, string entryId)
    {
        string preset = TrimOrEmpty(rawPreset);
        if (string.IsNullOrWhiteSpace(preset) ||
            preset.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            preset.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            preset.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            return MagicSummonQualityPreset.None;
        }

        if (preset.Equals("countByQuality", StringComparison.OrdinalIgnoreCase) ||
            preset.Equals("qualityCount", StringComparison.OrdinalIgnoreCase) ||
            preset.Equals("count", StringComparison.OrdinalIgnoreCase))
        {
            return MagicSummonQualityPreset.CountByQuality;
        }

        if (preset.Equals("levelByQuality", StringComparison.OrdinalIgnoreCase) ||
            preset.Equals("qualityLevel", StringComparison.OrdinalIgnoreCase) ||
            preset.Equals("level", StringComparison.OrdinalIgnoreCase))
        {
            return MagicSummonQualityPreset.LevelByQuality;
        }

        RuleDiagnostics.Warning(
            $"Ignoring invalid qualityPreset '{preset}' in summon override '{entryId}' from {SecondaryAttackYamlDomainRegistry.BloodMagicYamlFileName}. Valid values are countByQuality or levelByQuality.");
        return MagicSummonQualityPreset.None;
    }

    private static int? NormalizeLifetimeSeconds(int? rawLifetimeSeconds, string entryId)
    {
        if (!rawLifetimeSeconds.HasValue)
        {
            return null;
        }

        if (rawLifetimeSeconds.Value > 0)
        {
            return rawLifetimeSeconds.Value;
        }

        RuleDiagnostics.Warning(
            $"Ignoring invalid lifetimeSeconds '{rawLifetimeSeconds.Value}' in summon override '{entryId}' from {SecondaryAttackYamlDomainRegistry.BloodMagicYamlFileName}. The value must be a positive integer; the global Blood Magic summon lifetime will be used instead.");
        return null;
    }

    private static int ClampInt(int value, int min, int max)
    {
        return Math.Min(max, Math.Max(min, value));
    }

    private static string SanitizeMagicSummonId(string value)
    {
        string sanitized = new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        sanitized = sanitized.Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Summon" : sanitized;
    }
}

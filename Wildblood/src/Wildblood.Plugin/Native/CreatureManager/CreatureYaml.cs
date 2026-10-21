// SPDX-License-Identifier: GPL-3.0-only
// Portions from CreatureManager 1.2.5 (sighsorry1029), GPL-3.0
// 47e45a6e8c3b389a51e42fc7387d1a4fb90af473
// Changed by expDARE, 2026: Wildblood namespace and selected native dependency closure.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;
namespace ExtraPotions.Wildblood.AdoptedCm;
internal static class CreatureYaml
{
    internal enum ModifierYamlContext { Level, Karma }
    private static readonly HashSet<string> LevelFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "level",
        "damage",
        "damagePerLevel",
        "health",
        "healthPerLevel",
        "scalePerLevel",
        "distanceScaling",
        "modifiers",
        "modifierDistanceScaling"
    };

    internal static bool ValidateDefinitions<T>(IReadOnlyList<T> definitions, string source)
    {
        for (int index = 0; index < definitions.Count; index++)
        {
            object? definition = definitions[index];
            string path = $"{source}[{index + 1}]";
            if (definition == null)
            {
                return Invalid(path, "definition must be a mapping, not null.");
            }

            bool valid = definition switch
            {
                LevelDefinition level => ValidateNormalizedLevelDefinition(level, path),
                _ => true
            };
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
    private static bool ValidateNameList(List<string>? values, string path, string field)
    {
        if (values == null) return true;
        return values.All(value => !string.IsNullOrWhiteSpace(value)) || Invalid(path, $"{field} must contain only non-empty names.");
    }
    private static bool IsFinite(float? value, string path, string field)
    {
        return !value.HasValue || IsFinite(value.Value) || Invalid(path, $"{field} must be a finite number.");
    }
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
    private static bool TryParseFiniteFloat(string value, out float parsed)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) && IsFinite(parsed);
    }
    private static bool Invalid(string path, string message)
    {
        CreatureManagerPlugin.Log.LogError($"Failed to read YAML from {path}: {message}");
        return false;
    }
    internal static bool TryReadLevelDefinitions(string yaml, string source, out List<LevelDefinition> definitions)
    {
        definitions = new List<LevelDefinition>();
        if (string.IsNullOrWhiteSpace(yaml) || IsCommentOnlyYaml(yaml))
        {
            return true;
        }

        try
        {
            YamlStream stream = new();
            using StringReader reader = new(yaml);
            stream.Load(reader);
            if (stream.Documents.Count == 0)
            {
                return true;
            }

            if (stream.Documents.Count != 1)
            {
                throw new FormatException($"Level YAML from {source} must contain exactly one YAML document.");
            }

            YamlNode rootNode = stream.Documents[0].RootNode;
            if (rootNode is YamlSequenceNode emptySequence && emptySequence.Children.Count == 0)
            {
                return true;
            }

            if (rootNode is not YamlMappingNode root)
            {
                CreatureManagerPlugin.Log.LogError($"Failed to read level YAML from {source}: expected a top-level mapping or an empty sequence.");
                return false;
            }

            ValidateUniqueMappingKeys(root, source, "root");

            Dictionary<string, List<string>> groups = ReadLevelGroups(root, source);
            List<LevelDefinition> nestedDefinitions = ReadNestedLevelDefinitions(root, groups, source, out HashSet<string> targetsWithNestedDefinitions);
            definitions = ReadTopLevelLevelDefinitions(root, groups, source, targetsWithNestedDefinitions);
            definitions.AddRange(nestedDefinitions);
            return ValidateDefinitions(definitions, source);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogError($"Failed to read level YAML from {source}: {ex.Message}");
            definitions.Clear();
            return false;
        }
    }
    private static List<LevelDefinition> ReadTopLevelLevelDefinitions(YamlMappingNode root, Dictionary<string, List<string>> groups, string source, HashSet<string> targetsWithNestedDefinitions)
    {
        List<LevelDefinition> definitions = new();
        foreach (KeyValuePair<YamlNode, YamlNode> entry in root.Children)
        {
            string target = GetYamlScalar(entry.Key);
            if (target.Length == 0)
            {
                throw new FormatException($"Level YAML from {source} has an empty top-level block name.");
            }

            if (string.Equals(target, "groups", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsUnsupportedLevelTarget(target))
            {
                throw new FormatException($"Level YAML from {source} uses unsupported top-level block '{target}'. Use 'Boss'.");
            }

            if (entry.Value is not YamlMappingNode targetMap)
            {
                throw new FormatException($"Level YAML from {source} '{target}' must be a mapping.");
            }

            LevelDefinition rule = ReadLevelDefinition(targetMap, source, target, allowNestedDefinitions: true);
            rule.Target = target;
            if (groups.TryGetValue(target, out List<string>? prefabs) && prefabs != null)
            {
                rule.Prefabs = prefabs;
            }

            bool warnNoEffect = !targetsWithNestedDefinitions.Contains(target);
            if (ValidateLevelDefinition(rule, source, target, warnNoEffect))
            {
                definitions.Add(rule);
            }
        }

        return definitions;
    }
    private static Dictionary<string, List<string>> ReadLevelGroups(YamlMappingNode root, string source)
    {
        Dictionary<string, List<string>> groups = new(StringComparer.OrdinalIgnoreCase);
        YamlMappingNode? groupMap = null;
        foreach (KeyValuePair<YamlNode, YamlNode> rootEntry in root.Children)
        {
            if (!string.Equals(GetYamlScalar(rootEntry.Key), "groups", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            groupMap = rootEntry.Value as YamlMappingNode;
            if (groupMap == null)
            {
                throw new FormatException($"Level YAML from {source} 'groups' must be a mapping.");
            }

            break;
        }

        if (groupMap == null)
        {
            return groups;
        }

        foreach (KeyValuePair<YamlNode, YamlNode> entry in groupMap.Children)
        {
            string group = GetYamlScalar(entry.Key);
            if (group.Length == 0)
            {
                throw new FormatException($"Level YAML from {source} has an empty group name.");
            }

            if (entry.Value is not YamlSequenceNode sequence)
            {
                throw new FormatException($"Level YAML from {source} group '{group}' must be a prefab list.");
            }

            List<string> prefabs = new();
            foreach (YamlNode child in sequence.Children)
            {
                string prefab = GetYamlScalar(child);
                if (child is not YamlScalarNode || string.IsNullOrWhiteSpace(prefab))
                {
                    throw new FormatException($"Level YAML from {source} group '{group}' must contain only non-empty prefab names.");
                }

                if (!prefabs.Contains(prefab, StringComparer.OrdinalIgnoreCase))
                {
                    prefabs.Add(prefab);
                }
            }
            if (prefabs.Count == 0)
            {
                throw new FormatException($"Level YAML from {source} group '{group}' has no prefab names.");
            }

            groups[group] = prefabs;
        }

        return groups;
    }
    private static List<LevelDefinition> ReadNestedLevelDefinitions(YamlMappingNode root, Dictionary<string, List<string>> groups, string source, out HashSet<string> targetsWithNestedDefinitions)
    {
        List<LevelDefinition> definitions = new();
        targetsWithNestedDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<YamlNode, YamlNode> targetEntry in root.Children)
        {
            string target = GetYamlScalar(targetEntry.Key);
            if (target.Length == 0 || string.Equals(target, "groups", StringComparison.OrdinalIgnoreCase) ||
                IsUnsupportedLevelTarget(target) ||
                targetEntry.Value is not YamlMappingNode targetMap)
            {
                continue;
            }

            foreach (KeyValuePair<YamlNode, YamlNode> nestedEntry in targetMap.Children)
            {
                string biome = GetYamlScalar(nestedEntry.Key);
                if (biome.Length == 0 || LevelFieldNames.Contains(biome) || nestedEntry.Value is not YamlMappingNode nestedMap)
                {
                    continue;
                }

                targetsWithNestedDefinitions.Add(target);
                string label = $"{target}.{biome}";
                LevelDefinition rule = ReadLevelDefinition(nestedMap, source, label);
                rule.Target = target;
                rule.Biome = biome;
                if (groups.TryGetValue(target, out List<string>? prefabs) && prefabs != null)
                {
                    rule.Prefabs = prefabs;
                }

                if (ValidateLevelDefinition(rule, source, label, warnNoEffect: true))
                {
                    definitions.Add(rule);
                }
            }
        }

        return definitions;
    }
    private static bool IsUnsupportedLevelTarget(string target)
    {
        return string.Equals(target, "Bosses", StringComparison.OrdinalIgnoreCase);
    }
    internal static void ValidateUniqueMappingKeys(YamlNode node, string source, string path)
    {
        if (node is YamlMappingNode mapping)
        {
            HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
            {
                string key = GetYamlScalar(entry.Key);
                if (!keys.Add(key))
                {
                    throw new FormatException($"YAML from {source} {path} has duplicate key '{key}' when compared case-insensitively.");
                }

                ValidateUniqueMappingKeys(entry.Value, source, $"{path}.{key}");
            }

            return;
        }

        if (node is YamlSequenceNode sequence)
        {
            for (int index = 0; index < sequence.Children.Count; index++)
            {
                ValidateUniqueMappingKeys(sequence.Children[index], source, $"{path}[{index + 1}]");
            }
        }
    }
    private static LevelDefinition ReadLevelDefinition(
        YamlMappingNode node,
        string source,
        string label,
        bool allowNestedDefinitions = false)
    {
        LevelDefinition rule = new();
        foreach (KeyValuePair<YamlNode, YamlNode> entry in node.Children)
        {
            string field = GetYamlScalar(entry.Key);
            switch (field.ToLowerInvariant())
            {
                case "level":
                    rule.Level = ReadFloatList(entry.Value, source, label, "level");
                    break;
                case "damage":
                    rule.Damage = ReadFloat(entry.Value, source, label, "damage");
                    break;
                case "damageperlevel":
                    rule.DamagePerLevel = ReadFloat(entry.Value, source, label, "damagePerLevel");
                    break;
                case "health":
                    rule.Health = ReadFloat(entry.Value, source, label, "health");
                    break;
                case "healthperlevel":
                    rule.HealthPerLevel = ReadFloat(entry.Value, source, label, "healthPerLevel");
                    break;
                case "scaleperlevel":
                    rule.ScalePerLevel = ReadFloat(entry.Value, source, label, "scalePerLevel");
                    break;
                case "distancescaling":
                    rule.DistanceScaling = ReadFloatList(entry.Value, source, label, "distanceScaling");
                    break;
                case "modifierdistancescaling":
                    rule.ModifierDistanceScaling = ReadFloatList(entry.Value, source, label, "modifierDistanceScaling");
                    break;
                case "modifiers":
                    if (!TryReadModifierBlock(
                            entry.Value,
                            source,
                            label,
                            ModifierYamlContext.Level,
                            out Dictionary<string, ModifierDefinition>? modifiers,
                            out bool modifiersCleared))
                    {
                        throw new FormatException($"Level YAML from {source} '{label}'.modifiers is invalid.");
                    }
                    rule.Modifiers = modifiers;
                    rule.ModifiersCleared = modifiersCleared;
                    break;
                default:
                    if (allowNestedDefinitions && entry.Value is YamlMappingNode)
                    {
                        break;
                    }

                    throw new FormatException($"Level YAML from {source} '{label}' has unknown field '{field}'.");
            }
        }

        return rule;
    }
    private static List<float>? ReadFloatList(YamlNode node, string source, string label, string field)
    {
        if (node is not YamlSequenceNode sequence)
        {
            throw new FormatException($"Level YAML from {source} '{label}'.{field} must be a YAML list.");
        }

        List<float> values = new();
        foreach (YamlNode item in sequence.Children)
        {
            float? value = ReadFloat(item, source, label, field);
            if (!value.HasValue)
            {
                throw new FormatException($"Level YAML from {source} '{label}'.{field} contains an invalid number.");
            }

            values.Add(value.Value);
        }

        return values;
    }
    private static float? ReadFloat(YamlNode node, string source, string label, string field)
    {
        string text = GetYamlScalar(node);
        if (TryParseFiniteFloat(text, out float value))
        {
            return value;
        }

        throw new FormatException($"Level YAML from {source} '{label}'.{field} has invalid number '{text}'.");
    }
    internal static bool TryReadModifierBlock(
        YamlNode node,
        string source,
        string label,
        ModifierYamlContext context,
        out Dictionary<string, ModifierDefinition>? modifiers,
        out bool modifiersCleared)
    {
        modifiers = null;
        modifiersCleared = false;
        string blockPath = GetModifierBlockPath(source, label, context);
        if (node is YamlSequenceNode sequence)
        {
            if (sequence.Children.Count == 0)
            {
                modifiers = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
                modifiersCleared = true;
                return true;
            }

            CreatureManagerPlugin.Log.LogError($"{blockPath} must be a mapping ({{}} keeps fallback) or an empty list [] to block all lower modifier fallback.");
            return false;
        }

        if (node is not YamlMappingNode mapping)
        {
            CreatureManagerPlugin.Log.LogError($"{blockPath} must be a mapping ({{}} keeps fallback) or an empty list [] to block all lower modifier fallback.");
            return false;
        }

        modifiers = new Dictionary<string, ModifierDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<YamlNode, YamlNode> entry in mapping.Children)
        {
            string modifier = GetYamlScalar(entry.Key);
            if (context == ModifierYamlContext.Karma)
            {
                modifier = modifier.ToLowerInvariant();
            }

            if (modifier.Length == 0)
            {
                CreatureManagerPlugin.Log.LogError(context == ModifierYamlContext.Level
                    ? $"{blockPath} has an empty modifier name."
                    : $"{blockPath} has unknown modifier ''.");
                return false;
            }

            if (!CreatureModifierCatalog.IsKnown(modifier))
            {
                CreatureManagerPlugin.Log.LogError($"{blockPath} has unknown modifier '{modifier}'.");
                return false;
            }

            if (!TryReadModifierDefinition(entry.Value, source, label, modifier, context, out ModifierDefinition definition))
            {
                return false;
            }

            modifiers[modifier] = definition;
        }

        return true;
    }
    private static bool TryReadModifierDefinition(
        YamlNode node,
        string source,
        string label,
        string modifier,
        ModifierYamlContext context,
        out ModifierDefinition definition)
    {
        definition = null!;
        bool isBlink = modifier.Equals("blink", StringComparison.OrdinalIgnoreCase);
        bool isKnockback = modifier.Equals("juggernaut", StringComparison.OrdinalIgnoreCase);
        bool isChanceOnly = modifier.Equals("unflinching", StringComparison.OrdinalIgnoreCase);
        bool isUndodgeable = modifier.Equals("undodgeable", StringComparison.OrdinalIgnoreCase);
        bool isChameleon = modifier.Equals("chameleon", StringComparison.OrdinalIgnoreCase);
        bool isBlamer = modifier.Equals("blamer", StringComparison.OrdinalIgnoreCase);
        bool isDeathward = modifier.Equals("deathward", StringComparison.OrdinalIgnoreCase);
        bool isRegenerating = modifier.Equals("regenerating", StringComparison.OrdinalIgnoreCase);
        bool isReflection = modifier.Equals("reflection", StringComparison.OrdinalIgnoreCase);
        bool isReaping = modifier.Equals("reaping", StringComparison.OrdinalIgnoreCase);
        bool isStandardAffliction = modifier.Equals("exposed", StringComparison.OrdinalIgnoreCase) ||
                                    modifier.Equals("weakened", StringComparison.OrdinalIgnoreCase) ||
                                    modifier.Equals("withered", StringComparison.OrdinalIgnoreCase) ||
                                    modifier.Equals("corrosive", StringComparison.OrdinalIgnoreCase);
        bool isSplitAffliction = modifier.Equals("crippling", StringComparison.OrdinalIgnoreCase) ||
                                 modifier.Equals("disruptive", StringComparison.OrdinalIgnoreCase);
        bool isAdrenalineDrain = modifier.Equals("adrenalinedrain", StringComparison.OrdinalIgnoreCase);
        bool isToxicDeath = modifier.Equals("toxicdeath", StringComparison.OrdinalIgnoreCase);
        string format = GetModifierTupleFormat(
            isBlink,
            isKnockback,
            isChanceOnly,
            isUndodgeable,
            isChameleon,
            isBlamer,
            isDeathward,
            isRegenerating,
            isReflection,
            isReaping,
            isStandardAffliction,
            isSplitAffliction,
            isAdrenalineDrain,
            isToxicDeath);
        string modifierPath = $"{GetModifierBlockPath(source, label, context)}.{modifier}";
        string text = GetYamlScalar(node);
        if (text.Length == 0)
        {
            CreatureManagerPlugin.Log.LogWarning(context == ModifierYamlContext.Level
                ? $"{modifierPath} must be '{format}'."
                : $"{modifierPath} must be {format}.");
            return false;
        }

        string[] tokens = SplitTuplePreserveEmpty(text);
        int maxValues = isChanceOnly ? 1 :
            isReaping ? 9 :
            isSplitAffliction || isAdrenalineDrain ? 5 :
            isBlink || isDeathward || isBlamer || isStandardAffliction || isToxicDeath ? 4 :
            isRegenerating || isKnockback || isReflection ? 3 : 2;
        bool invalidCount = tokens.Length < 1 || tokens.Length > maxValues;
        if (invalidCount)
        {
            if (context == ModifierYamlContext.Level)
            {
                string expected = maxValues == 1
                    ? "1"
                    : $"1 to {maxValues.ToString(CultureInfo.InvariantCulture)}";
                CreatureManagerPlugin.Log.LogWarning(
                    $"{modifierPath} expected {expected} tuple values but got {tokens.Length}. Use modifierDistanceScaling for distance-based chance scaling.");
            }
            else
            {
                CreatureManagerPlugin.Log.LogWarning($"{modifierPath} must be {format}.");
            }

            return false;
        }

        int emptyIndex = Array.FindIndex(tokens, static token => token.Length == 0);
        if (emptyIndex >= 0)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} has an empty tuple value at position {emptyIndex + 1}. " +
                "Only trailing values may be omitted; end the tuple after the last supplied value.");
            return false;
        }

        int numericCount = isBlink || isToxicDeath || isDeathward ? Math.Min(tokens.Length, 3) : tokens.Length;
        float[] values = new float[numericCount];
        for (int index = 0; index < numericCount; index++)
        {
            if (TryParseFiniteFloat(tokens[index], out values[index]))
            {
                continue;
            }

            CreatureManagerPlugin.Log.LogError(
                $"{modifierPath} has invalid number '{tokens[index]}' at position {index + 1}.");
            return false;
        }

        int? deathwardMaxActivations = null;
        if (isDeathward && tokens.Length >= 4)
        {
            if (!int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedMaxActivations) ||
                parsedMaxActivations < 1)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"{modifierPath} maxActivations must be an integer of 1 or greater.");
                return false;
            }

            deathwardMaxActivations = parsedMaxActivations;
        }

        int? reapingHealMaxActivations = null;
        if (isReaping && tokens.Length >= 3)
        {
            if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedHealMaxActivations) ||
                parsedHealMaxActivations < 1)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"{modifierPath} healMaxActivations must be an integer of 1 or greater.");
                return false;
            }

            reapingHealMaxActivations = parsedHealMaxActivations;
        }

        definition = new ModifierDefinition
        {
            Chance = values[0]
        };
        if (values.Length >= 2)
        {
            if (isBlink)
            {
                definition.Cooldown = values[1];
            }
            else
            {
                definition.Power = values[1];
            }
        }

        if (isDeathward && values.Length >= 3)
        {
            definition.Cooldown = values[2];
        }

        if (isDeathward && deathwardMaxActivations.HasValue)
        {
            definition.MaxActivations = deathwardMaxActivations;
        }

        if (isRegenerating && values.Length >= 3)
        {
            definition.RegeneratingHealthPerSecondCap = values[2];
        }

        if (isKnockback && values.Length >= 3)
        {
            definition.Cooldown = values[2];
        }

        if (isBlamer && values.Length >= 3)
        {
            definition.MaxKarmaGain = values[2];
        }

        if (isBlamer && values.Length >= 4)
        {
            definition.FleeHealthRatio = UnityEngine.Mathf.Clamp01(values[3]);
        }

        if (isBlink && values.Length >= 3)
        {
            definition.MaxRange = values[2];
        }

        if (isBlink && tokens.Length >= 4)
        {
            definition.StartEffect = tokens[3];
        }

        if (isReaping && reapingHealMaxActivations.HasValue)
        {
            definition.ReapingHealMaxActivations = reapingHealMaxActivations;
        }

        if (isReaping && values.Length >= 4)
        {
            definition.ReapingMaxHealthPerKill = values[3];
        }

        if (isReaping && values.Length >= 5)
        {
            definition.ReapingMaxHealthCap = values[4];
        }

        if (isReaping && values.Length >= 6)
        {
            definition.ReapingDamagePerKill = values[5];
        }

        if (isReaping && values.Length >= 7)
        {
            definition.ReapingDamageCap = values[6];
        }

        if (isReaping && values.Length >= 8)
        {
            definition.ReapingScalePerKill = values[7];
        }

        if (isReaping && values.Length >= 9)
        {
            definition.ReapingScaleCap = values[8];
        }

        if (isStandardAffliction && values.Length >= 3)
        {
            definition.ProcChance = values[2];
        }

        if (isStandardAffliction && values.Length >= 4)
        {
            definition.Duration = values[3];
        }

        if ((isSplitAffliction || isAdrenalineDrain) && values.Length >= 3)
        {
            definition.SecondaryPower = values[2];
        }

        if ((isSplitAffliction || isAdrenalineDrain) && values.Length >= 4)
        {
            definition.ProcChance = values[3];
        }

        if ((isSplitAffliction || isAdrenalineDrain) && values.Length >= 5)
        {
            definition.Duration = values[4];
        }

        if (isReflection && values.Length >= 3)
        {
            definition.ProcChance = values[2];
        }

        if (isToxicDeath && values.Length >= 3)
        {
            definition.Radius = values[2];
        }

        if (isToxicDeath && tokens.Length >= 4)
        {
            definition.TriggerEffect = tokens[3];
        }

        if (context == ModifierYamlContext.Level)
        {
            ValidateLevelModifier(definition, modifier, modifierPath);
        }
        else
        {
            ValidateAndNormalizeKarmaModifier(definition, modifier, modifierPath);
        }

        return true;
    }
    private static string GetModifierBlockPath(string source, string label, ModifierYamlContext context)
    {
        return context == ModifierYamlContext.Level
            ? $"Level YAML from {source} '{label}'.modifiers"
            : $"Karma YAML from {source} {label}";
    }
    private static string GetModifierTupleFormat(
        bool isBlink,
        bool isKnockback,
        bool isChanceOnly,
        bool isUndodgeable,
        bool isChameleon,
        bool isBlamer,
        bool isDeathward,
        bool isRegenerating,
        bool isReflection,
        bool isReaping,
        bool isStandardAffliction,
        bool isSplitAffliction,
        bool isAdrenalineDrain,
        bool isToxicDeath)
    {
        if (isBlink) return "chance[, cooldown[, maxRange[, startEffect]]]";
        if (isUndodgeable) return "chance[, damageReduction]";
        if (isChanceOnly) return "chance";
        if (isKnockback) return "chance[, minimumPushForce[, cooldown]]";
        if (isDeathward) return "chance[, restoreHealth[, cooldown[, maxActivations]]]";
        if (isRegenerating) return "chance[, maxHealthRatioPerSecond[, healthPerSecondCap]]";
        if (isChameleon) return "chance[, immunitySwitchSeconds]";
        if (isBlamer) return "chance[, karmaPerSecond[, maxKarmaGain[, fleeHealthRatio]]]";
        if (isReaping) return "chance[, healPerKill[, healMaxActivations[, maxHealthPerKill[, maxHealthCap[, damagePerKill[, damageCap[, scalePerKill[, scaleCap]]]]]]]]";
        if (isStandardAffliction) return "chance[, power[, procChance[, duration]]]";
        if (isSplitAffliction) return "chance[, primaryPower[, secondaryPower[, procChance[, duration]]]]";
        if (isAdrenalineDrain) return "chance[, currentAdrenalineRemoved[, adrenalineGainReduction[, procChance[, duration]]]]";
        if (isReflection) return "chance[, reflectedDamage[, procChance]]";
        if (isToxicDeath) return "chance[, maxHealthDamage[, radius[, triggerEffect]]]";
        return "chance[, power]";
    }
    private static void ValidateAndNormalizeKarmaModifier(
        ModifierDefinition definition,
        string modifier,
        string modifierPath)
    {
        if (modifier.Equals("undodgeable", StringComparison.OrdinalIgnoreCase) &&
            definition.Power is < 0f or > 1f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} damageReduction must be from 0 to 1 and will be clamped at runtime.");
        }

        definition.Chance = UnityEngine.Mathf.Clamp(definition.Chance!.Value, 0f, 100f);
        if (definition.Cooldown.HasValue) definition.Cooldown = UnityEngine.Mathf.Max(0f, definition.Cooldown.Value);
        if (definition.MaxRange.HasValue) definition.MaxRange = UnityEngine.Mathf.Max(0f, definition.MaxRange.Value);
        if (definition.ProcChance.HasValue) definition.ProcChance = UnityEngine.Mathf.Clamp01(definition.ProcChance.Value);
        if (definition.Duration.HasValue) definition.Duration = UnityEngine.Mathf.Max(0.1f, definition.Duration.Value);
        if (definition.SecondaryPower.HasValue) definition.SecondaryPower = UnityEngine.Mathf.Clamp01(definition.SecondaryPower.Value);
        if (definition.Radius.HasValue) definition.Radius = UnityEngine.Mathf.Max(0f, definition.Radius.Value);
        if (definition.MaxKarmaGain.HasValue) definition.MaxKarmaGain = UnityEngine.Mathf.Max(0f, definition.MaxKarmaGain.Value);
        if (definition.FleeHealthRatio.HasValue) definition.FleeHealthRatio = UnityEngine.Mathf.Clamp01(definition.FleeHealthRatio.Value);
        if (definition.RegeneratingHealthPerSecondCap.HasValue) definition.RegeneratingHealthPerSecondCap = UnityEngine.Mathf.Max(0f, definition.RegeneratingHealthPerSecondCap.Value);
        if (definition.ReapingMaxHealthPerKill.HasValue) definition.ReapingMaxHealthPerKill = UnityEngine.Mathf.Max(0f, definition.ReapingMaxHealthPerKill.Value);
        if (definition.ReapingMaxHealthCap.HasValue) definition.ReapingMaxHealthCap = UnityEngine.Mathf.Max(0f, definition.ReapingMaxHealthCap.Value);
        if (definition.ReapingDamagePerKill.HasValue) definition.ReapingDamagePerKill = UnityEngine.Mathf.Max(0f, definition.ReapingDamagePerKill.Value);
        if (definition.ReapingDamageCap.HasValue) definition.ReapingDamageCap = UnityEngine.Mathf.Max(0f, definition.ReapingDamageCap.Value);
        if (definition.ReapingScalePerKill.HasValue) definition.ReapingScalePerKill = UnityEngine.Mathf.Max(0f, definition.ReapingScalePerKill.Value);
        if (definition.ReapingScaleCap.HasValue) definition.ReapingScaleCap = UnityEngine.Mathf.Max(0f, definition.ReapingScaleCap.Value);
    }
    private static void ValidateLevelModifier(
        ModifierDefinition definition,
        string modifier,
        string modifierPath)
    {
        if (definition.Chance is < 0f or > 100f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} chance must be from 0 to 100 and will be clamped at runtime.");
        }

        if (definition.Power.HasValue &&
            !modifier.Equals("juggernaut", StringComparison.OrdinalIgnoreCase) &&
            !modifier.Equals("chameleon", StringComparison.OrdinalIgnoreCase) &&
            definition.Power.Value is < 0f or > 1f)
        {
            string valueName = modifier.Equals("undodgeable", StringComparison.OrdinalIgnoreCase)
                ? "damageReduction"
                : "power";
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} {valueName} must be from 0 to 1 and will be clamped at runtime.");
        }

        if (modifier.Equals("chameleon", StringComparison.OrdinalIgnoreCase) && definition.Power.HasValue)
        {
            float interval = definition.Power.Value;
            string chameleonPath = GetCanonicalModifierPath(modifierPath, modifier, "chameleon");
            if (float.IsNaN(interval) || float.IsInfinity(interval))
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"{chameleonPath} immunitySwitchSeconds must be a finite number and will use the default interval.");
            }
            else if (interval <= 0f)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"{chameleonPath} immunitySwitchSeconds must be greater than 0 and will use at least 0.1 seconds.");
            }
        }

        if (modifier.Equals("juggernaut", StringComparison.OrdinalIgnoreCase) && definition.Power < 0f)
        {
            string juggernautPath = GetCanonicalModifierPath(modifierPath, modifier, "juggernaut");
            CreatureManagerPlugin.Log.LogWarning(
                $"{juggernautPath} minimumPushForce must be 0 or greater and will be clamped at runtime.");
        }

        if (definition.SecondaryPower.HasValue && definition.SecondaryPower.Value is < 0f or > 1f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} secondary power must be from 0 to 1 and will be clamped at runtime.");
        }

        if (definition.ProcChance.HasValue && definition.ProcChance.Value is < 0f or > 1f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} procChance must be from 0 to 1 and will be clamped at runtime.");
        }

        if (definition.Duration.HasValue && definition.Duration.Value <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} duration must be greater than 0 and will use at least 0.1 seconds.");
        }

        if (definition.Radius.HasValue && definition.Radius.Value < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} radius must be 0 or greater and will be clamped at runtime.");
        }

        if (definition.Cooldown.HasValue && definition.Cooldown.Value < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} cooldown must be 0 or greater and will be clamped at runtime.");
        }

        if (definition.MaxActivations.HasValue && definition.MaxActivations.Value < 1)
        {
            CreatureManagerPlugin.Log.LogWarning($"{modifierPath} maxActivations must be 1 or greater.");
        }

        if (definition.MaxRange.HasValue && definition.MaxRange.Value <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning(
                $"{modifierPath} maxRange must be greater than 0 and will disable Blink AI range expansion.");
        }

        if (modifier.Equals("regenerating", StringComparison.OrdinalIgnoreCase) &&
            definition.RegeneratingHealthPerSecondCap < 0f)
        {
            string regeneratingPath = GetCanonicalModifierPath(modifierPath, modifier, "regenerating");
            CreatureManagerPlugin.Log.LogWarning(
                $"{regeneratingPath} healthPerSecondCap must be 0 or greater and will be clamped at runtime.");
        }

        if (modifier.Equals("reaping", StringComparison.OrdinalIgnoreCase) &&
            (definition.ReapingMaxHealthPerKill < 0f ||
             definition.ReapingMaxHealthCap < 0f ||
             definition.ReapingDamagePerKill < 0f ||
             definition.ReapingDamageCap < 0f ||
             definition.ReapingScalePerKill < 0f ||
             definition.ReapingScaleCap < 0f))
        {
            string reapingPath = GetCanonicalModifierPath(modifierPath, modifier, "reaping");
            CreatureManagerPlugin.Log.LogWarning(
                $"{reapingPath} gain and cap values must be 0 or greater and will be clamped at runtime.");
        }
    }
    private static string GetCanonicalModifierPath(string modifierPath, string modifier, string canonicalModifier)
    {
        return modifierPath.Substring(0, modifierPath.Length - modifier.Length) + canonicalModifier;
    }
    private static string GetYamlScalar(YamlNode node)
    {
        return node is YamlScalarNode scalar ? (scalar.Value ?? "").Trim() : "";
    }
    private static bool ValidateNormalizedLevelDefinition(LevelDefinition rule, string path)
    {
        if (string.IsNullOrWhiteSpace(rule.Target))
        {
            return Invalid(path, "target must be a non-empty name.");
        }

        if (rule.Prefabs != null && !ValidateNameList(rule.Prefabs, path, "prefabs"))
        {
            return false;
        }

        if (rule.ModifiersCleared && rule.Modifiers is { Count: > 0 })
        {
            return Invalid(path, "modifiersCleared cannot be combined with modifier entries.");
        }

        if (rule.Modifiers != null)
        {
            foreach (KeyValuePair<string, ModifierDefinition> entry in rule.Modifiers)
            {
                if (!CreatureModifierCatalog.IsKnown(entry.Key))
                {
                    return Invalid(path, $"modifiers has unknown modifier '{entry.Key}'.");
                }

                if (!ValidateNormalizedModifier(entry.Key, entry.Value, path))
                {
                    return false;
                }
            }
        }

        try
        {
            if (!ValidateLevelDefinition(rule, path, rule.Target!, warnNoEffect: true))
            {
                return Invalid(path, "level definition has no effect fields.");
            }
        }
        catch (Exception ex)
        {
            return Invalid(path, ex.Message);
        }

        return true;
    }
    private static bool ValidateNormalizedModifier(string modifier, ModifierDefinition? definition, string path)
    {
        if (definition == null || !definition.Chance.HasValue || !IsFinite(definition.Chance.Value))
        {
            return Invalid(path, $"modifiers.{modifier}.chance must be a finite number.");
        }

        float?[] floats =
        {
            definition.Power, definition.Cooldown, definition.MaxRange, definition.ProcChance,
            definition.Duration, definition.SecondaryPower, definition.Radius, definition.MaxKarmaGain,
            definition.FleeHealthRatio, definition.RegeneratingHealthPerSecondCap,
            definition.ReapingMaxHealthPerKill, definition.ReapingMaxHealthCap,
            definition.ReapingDamagePerKill, definition.ReapingDamageCap, definition.ReapingScalePerKill,
            definition.ReapingScaleCap
        };
        if (floats.Any(value => value.HasValue && !IsFinite(value.Value)))
        {
            return Invalid(path, $"modifiers.{modifier} must contain only finite numbers.");
        }

        if (definition.MaxActivations is < 1 || definition.ReapingHealMaxActivations is < 1)
        {
            return Invalid(path, $"modifiers.{modifier} activation counts must be integers of 1 or greater.");
        }

        ValidateLevelModifier(definition, modifier, $"{path}.modifiers.{modifier}");
        return true;
    }
    private static bool ValidateLevelDefinition(LevelDefinition rule, string source, string label, bool warnNoEffect)
    {
        if (rule.Level is { Count: 0 })
        {
            throw new FormatException($"Level YAML from {source} '{label}'.level must not be empty.");
        }

        if (rule.Level != null && rule.Level.Any(value => !IsFinite(value)))
            throw new FormatException($"Level YAML from {source} '{label}'.level must contain only finite numbers.");

        if (rule.Health.HasValue && (!IsFinite(rule.Health.Value) || rule.Health.Value <= 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.health must be a finite number greater than 0.");
        }

        if (rule.Damage.HasValue && (!IsFinite(rule.Damage.Value) || rule.Damage.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.damage must be a finite number of 0 or greater.");
        }

        if (rule.DamagePerLevel.HasValue && (!IsFinite(rule.DamagePerLevel.Value) || rule.DamagePerLevel.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.damagePerLevel must be a finite number of 0 or greater.");
        }

        if (rule.HealthPerLevel.HasValue && (!IsFinite(rule.HealthPerLevel.Value) || rule.HealthPerLevel.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.healthPerLevel must be a finite number of 0 or greater.");
        }

        if (rule.ScalePerLevel.HasValue && (!IsFinite(rule.ScalePerLevel.Value) || rule.ScalePerLevel.Value < 0f))
        {
            throw new FormatException($"Level YAML from {source} '{label}'.scalePerLevel must be a finite number of 0 or greater.");
        }

        ValidateDistanceScaling(rule.DistanceScaling, source, label);
        ValidateModifierDistanceScaling(rule.ModifierDistanceScaling, source, label);
        if (rule.Level == null && !rule.Health.HasValue && !rule.HealthPerLevel.HasValue &&
            !rule.Damage.HasValue && !rule.DamagePerLevel.HasValue && !rule.ScalePerLevel.HasValue &&
            !HasDistanceScalingEffect(rule.DistanceScaling) &&
            !HasModifierDistanceScalingValue(rule.ModifierDistanceScaling) &&
            !rule.ModifiersCleared &&
            (rule.Modifiers == null || rule.Modifiers.Count == 0))
        {
            if (warnNoEffect && rule.Modifiers == null)
            {
                CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}' has no effect fields and will be ignored.");
            }

            return false;
        }

        return true;
    }
    private static bool HasDistanceScalingEffect(List<float>? scaling)
    {
        return scaling is { Count: >= 2 } && (scaling[0] > 0f || scaling[1] > 0f);
    }
    private static void ValidateDistanceScaling(List<float>? scaling, string source, string label)
    {
        if (scaling == null)
        {
            return;
        }

        if (scaling.Count is < 2 or > 4)
        {
            throw new FormatException($"Level YAML from {source} '{label}'.distanceScaling must be [damage, health[, interval[, maxSteps]]].");
        }

        if (scaling.Any(value => !IsFinite(value)))
            throw new FormatException($"Level YAML from {source} '{label}'.distanceScaling must contain only finite numbers.");

        if (scaling[0] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling damage must be 0 or greater.");
            scaling[0] = 0f;
        }

        if (scaling[1] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling health must be 0 or greater.");
            scaling[1] = 0f;
        }

        if (scaling.Count >= 3 && scaling[2] <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling interval must be greater than 0 and will use 1000.");
            scaling[2] = 1000f;
        }

        if (scaling.Count >= 4 && scaling[3] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.distanceScaling maxSteps must be 0 or greater and will be ignored.");
            scaling[3] = 0f;
        }
    }
    private static bool HasModifierDistanceScalingValue(List<float>? scaling)
    {
        return scaling is { Count: 3 };
    }
    private static void ValidateModifierDistanceScaling(List<float>? scaling, string source, string label)
    {
        if (scaling == null)
        {
            return;
        }

        if (scaling.Count != 3)
        {
            throw new FormatException($"Level YAML from {source} '{label}'.modifierDistanceScaling must be [chancePerStep, stepDistance, maxSteps].");
        }

        if (scaling.Any(value => !IsFinite(value)))
            throw new FormatException($"Level YAML from {source} '{label}'.modifierDistanceScaling must contain only finite numbers.");

        if (scaling[0] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.modifierDistanceScaling chancePerStep must be 0 or greater.");
            scaling[0] = 0f;
        }

        if (scaling[1] <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.modifierDistanceScaling stepDistance must be greater than 0 and will use 1000.");
            scaling[1] = 1000f;
        }

        if (scaling[2] < 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level YAML from {source} '{label}'.modifierDistanceScaling maxSteps must be 0 or greater and will be ignored.");
            scaling[2] = 0f;
        }
    }
    private static bool IsCommentOnlyYaml(string yaml)
    {
        foreach (string line in yaml.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
    private static string[] SplitTuplePreserveEmpty(string line)
    {
        return line
            .Split(',')
            .Select(CleanTupleToken)
            .ToArray();
    }
    private static string CleanTupleToken(string token)
    {
        string cleaned = token.Trim();
        if (cleaned.Length >= 2 && cleaned[0] == '\'' && cleaned[cleaned.Length - 1] == '\'')
        {
            return cleaned.Substring(1, cleaned.Length - 2).Replace("''", "'");
        }

        if (cleaned.Length >= 2 && cleaned[0] == '"' && cleaned[cleaned.Length - 1] == '"')
        {
            return cleaned.Substring(1, cleaned.Length - 2).Replace("\\\"", "\"");
        }

        return cleaned;
    }

}

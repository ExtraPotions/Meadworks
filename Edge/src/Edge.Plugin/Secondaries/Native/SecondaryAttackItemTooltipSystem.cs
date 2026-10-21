// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8.
// Changed by expDARE, 2026: vanilla row ownership, Edge gates and C05 translations; source algorithms retained.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Edge.Secondaries.Adopted;
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

internal static class SecondaryAttackItemTooltipSystem
{
    private const string HeadingColor = "#FFD27A";
    private const string DescriptionColor = "#D6D6D6";

    internal static void AppendPresetDescriptions(ItemDrop.ItemData item, ref string tooltip)
    {
        bool hasAttackDefinition =
            SecondaryAttackRuntimeFacade.TryGetDefinition(item, out SecondaryAttackDefinition definition);
        bool hasSummonQualityPreset =
            MagicSummonQualityPresetSystem.TryGetItemQualityPreset(
                item,
                out MagicSummonQualityPreset qualityPreset);
        if (!hasAttackDefinition && !hasSummonQualityPreset)
        {
            return;
        }

        int expectedPresetCount = (hasAttackDefinition ? 3 : 0) + (hasSummonQualityPreset ? 1 : 0);
        List<string> presetKeys = new(expectedPresetCount);
        HashSet<string> seenPresetKeys = new(StringComparer.OrdinalIgnoreCase);

        if (hasAttackDefinition)
        {
            AddPresetKey(
                presetKeys,
                seenPresetKeys,
                NativePresetPresentation.GetKey(definition.Behavior));
            AddCompiledMeleePresetKeys(definition, presetKeys, seenPresetKeys);
            if (definition.HarvestSweep != null &&
                definition.SpinningSweep == null)
            {
                AddPresetKey(presetKeys, seenPresetKeys, "harvestSweep");
            }
        }

        if (hasSummonQualityPreset)
        {
            AddPresetKey(
                presetKeys,
                seenPresetKeys,
                qualityPreset == MagicSummonQualityPreset.CountByQuality
                    ? "countByQuality"
                    : "levelByQuality");
        }

        string presetTooltip = BuildPresetTooltip(presetKeys);
        if (hasAttackDefinition) presetTooltip += ReadRuntimeCosts(item, definition, presetKeys);
        if (string.IsNullOrEmpty(presetTooltip))
        {
            return;
        }

        if (!string.IsNullOrEmpty(tooltip) &&
            tooltip.IndexOf(presetTooltip, StringComparison.Ordinal) >= 0)
        {
            return;
        }

        string separator = string.IsNullOrEmpty(tooltip)
            ? ""
            : tooltip.EndsWith("\n\n", StringComparison.Ordinal)
                ? ""
                : tooltip.EndsWith("\n", StringComparison.Ordinal)
                    ? "\n"
                    : "\n\n";
        tooltip = string.IsNullOrEmpty(tooltip)
            ? presetTooltip
            : $"{tooltip}{separator}{presetTooltip}";
    }

    // Query an isolated configured attack; never mutate the prefab, held attack, inventory item or resource balances.
    private static string ReadRuntimeCosts(ItemDrop.ItemData item, SecondaryAttackDefinition definition, IReadOnlyList<string> presetKeys)
    {
        var player = Player.m_localPlayer;
        if (player == null || definition.ConfiguredSecondaryAttack == null) return "";
        var attack = SecondaryAttackManager.CloneAttack(definition.ConfiguredSecondaryAttack);
        attack.m_character = player; attack.m_weapon = item;
        var text = new StringBuilder();
        void Cost(string key, float value) { if (value > 0) text.Append("\n").Append(EdgeText.Format(key, value.ToString("0.0"))); }
        Cost("tooltip_stamina", attack.GetAttackStamina());
        Cost("tooltip_eitr", attack.GetAttackEitr());
        Cost("tooltip_health", attack.GetAttackHealth());
        foreach (var key in presetKeys)
        {
            var cooldown = NativePresetPresentation.CooldownFor(definition, key);
            var seconds = key == NativePresetPresentation.GetKey(definition.Behavior) && definition.Behavior is ProjectileSecondaryBehavior ranged
                ? RangedSecondaryCooldownSystem.ReadDuration(player, item, ranged)
                : cooldown == null ? 0 : MeleePresetCooldownSystem.ReadDuration(player, item, cooldown);
            if (seconds > 0) text.Append("\n").Append(EdgeText.Format("tooltip_cooldown", seconds.ToString("0.0")));
        }
        return text.ToString();
    }

    internal static IReadOnlyList<string> PresetKeys(SecondaryAttackDefinition definition)
    {
        var keys = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPresetKey(keys, seen, NativePresetPresentation.GetKey(definition.Behavior));
        AddCompiledMeleePresetKeys(definition, keys, seen);
        if (definition.HarvestSweep != null && definition.SpinningSweep == null) AddPresetKey(keys, seen, "harvestSweep");
        return keys;
    }

    private static void AddCompiledMeleePresetKeys(
        SecondaryAttackDefinition definition,
        ICollection<string> presetKeys,
        ISet<string> seenPresetKeys)
    {
        if (definition.SneakAmbush != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "sneakAmbush");
        }

        if (definition.CleavingThrust != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "cleavingThrust");
        }

        if (definition.LaunchSlam != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "launchSlam");
        }

        if (definition.KnockbackChain != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "knockbackChain");
        }

        if (definition.Aftershock != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "aftershock");
        }

        if (definition.RiftTrail != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "riftTrail");
        }

        if (definition.FractureLine != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "fractureLine");
        }

        if (definition.BehaviorType == SecondaryAttackBehaviorType.CopiedSecondary)
        {
            AddPresetKey(
                presetKeys,
                seenPresetKeys,
                definition.OnProjectileHit == null
                    ? null
                    : NativePresetPresentation.GetKey(definition.OnProjectileHit.Preset));
            if (definition.Boomerang != null)
            {
                AddPresetKey(presetKeys, seenPresetKeys, "boomerang");
            }
        }

        if (definition.SpinningSweep != null)
        {
            AddPresetKey(presetKeys, seenPresetKeys, "spinningSweep");
        }
    }

    private static void AddPresetKey(
        ICollection<string> presetKeys,
        ISet<string> seenPresetKeys,
        string? presetKey)
    {
        if (string.IsNullOrWhiteSpace(presetKey))
        {
            return;
        }

        string resolvedPresetKey = presetKey!.Trim();
        if (seenPresetKeys.Add(resolvedPresetKey))
        {
            presetKeys.Add(resolvedPresetKey);
        }
    }

    private static string BuildPresetTooltip(IReadOnlyList<string> presetKeys)
    {
        StringBuilder builder = new();
        foreach (string presetKey in presetKeys)
        {
            if (!PresetCatalog.TryGet(presetKey, out PresetInfo presetInfo))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            bool summonQuality = presetInfo.Group == PresetGroup.SummonQuality;
            string heading = EdgeText.Text(summonQuality ? "tooltip_summon_quality" : "hint_secondary");
            string presetName = EdgeText.Text(presetInfo.NameKey);
            string presetDescription = EdgeText.Text(presetInfo.DescriptionKey);

            builder
                .Append("<color=")
                .Append(HeadingColor)
                .Append("><b>")
                .Append(heading)
                .Append(": ")
                .Append(presetName)
                .Append("</b></color>\n<color=")
                .Append(DescriptionColor)
                .Append('>')
                .Append(presetDescription)
                .Append("</color>");
        }

        return builder.ToString();
    }
}

[HarmonyPatch(
    typeof(ItemDrop.ItemData),
    nameof(ItemDrop.ItemData.GetTooltip),
    new[]
    {
        typeof(ItemDrop.ItemData),
        typeof(int),
        typeof(bool),
        typeof(float),
        typeof(int),
        typeof(bool)
    })]
[HarmonyAfter(
    "randyknapp.mods.epicloot",
    "org.bepinex.plugins.jewelcrafting",
    "MidnightsFX.EpicJewels")]
internal static class ItemDataGetTooltipSecondaryAttackPatch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        ItemDrop.ItemData item,
        bool crafting,
        bool appending,
        ref string __result)
    {
        if (crafting || appending ||
            item == null ||
            !Plugin.AllowsUi || !EdgeModules.Active(EdgeModule.Secondaries) || !(Plugin.Settings?.Get(EdgeSettings.Interface.SecondaryTooltips) ?? false))
        {
            return;
        }

        SecondaryAttackItemTooltipSystem.AppendPresetDescriptions(item, ref __result);
    }
}

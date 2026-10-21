// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Edge-owned CS1 page content. Saga chooses presentation; no experience producer is registered here.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Runtime.Secondaries.Native;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Edge.Secondaries;
using UnityEngine;

namespace ExtraPotions.Edge.Runtime;

internal static class EdgeSkillPages
{
    private static readonly List<IDisposable> pages = new();
    internal static void Initialize()
    {
        Dispose();
        foreach (var skill in new[] { Skills.SkillType.Swords, Skills.SkillType.Knives, Skills.SkillType.Clubs, Skills.SkillType.Polearms,
            Skills.SkillType.Spears, Skills.SkillType.Blocking, Skills.SkillType.Axes, Skills.SkillType.Bows, Skills.SkillType.ElementalMagic,
            Skills.SkillType.Unarmed, Skills.SkillType.Pickaxes, Skills.SkillType.Crossbows, Skills.SkillType.BloodMagic, Skills.SkillType.Sneak,
            Skills.SkillType.WoodCutting, Skills.SkillType.Fishing, Skills.SkillType.Farming })
        {
            var selected = skill;
            pages.Add(MeadworksSkills.RegisterPage(Plugin.Guid, selected, (player, level) => Page(player, selected, level)));
        }
    }
    internal static void Dispose() { foreach (var page in pages) page.Dispose(); pages.Clear(); }
    private static IReadOnlyList<SkillPageLine> Page(Player player, Skills.SkillType skill, float level)
    {
        var settings = Plugin.Settings;
        if (settings == null || !Plugin.AllowsUi || !settings.Get(EdgeSettings.General.Enabled) || !EdgeModules.ProductActive(out _)) return Array.Empty<SkillPageLine>();
        var lines = new List<SkillPageLine>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string text) { if (seen.Add(text)) lines.Add(new SkillPageLine(text)); }
        if (skill == Skills.SkillType.Blocking && Combat.DodgeCancel.Enabled) Add(EdgeText.Text("page_dodge_cancel"));
        if (!EdgeModules.Active(EdgeModule.Secondaries))
        {
            if (lines.Count > 0) lines.Insert(0, new SkillPageLine(Plugin.Name, heading: true));
            return lines;
        }
        if (skill == Skills.SkillType.BloodMagic)
        {
            Add(EdgeText.Text(settings.Get(EdgeSettings.BloodMagic.HealthCostUsesMaxHealth) ? "page_health_max" : "page_health_current"));
            Add(EdgeText.Format("page_health_experience", settings.Get(EdgeSettings.BloodMagic.HealthCostSkillRaiseFactor).ToString("0.###")));
            int seconds = settings.Get(EdgeSettings.BloodMagic.SummonLifetimeSeconds);
            if (seconds > 0) Add(EdgeText.Format("page_summon_lifetime", MagicSummonQualityPresetSystem.ReadLifetimeSeconds(seconds, level / 100f).ToString("0")));
        }
        foreach (var pair in EdgeNativeDefinitions.Applied.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var definition = pair.Value;
            var item = ObjectDB.instance?.GetItemPrefab(pair.Key)?.GetComponent<ItemDrop>()?.m_itemData;
            if (item?.m_shared == null) continue;
            foreach (string key in SecondaryAttackItemTooltipSystem.PresetKeys(definition))
            {
                if (!PresetCatalog.TryGet(key, out var preset)) continue;
                var ranged = key == NativePresetPresentation.GetKey(definition.Behavior) ? definition.Behavior as ProjectileSecondaryBehavior : null;
                var cooldown = NativePresetPresentation.CooldownFor(definition, key);
                float duration = ranged?.Cooldown ?? cooldown?.Cooldown ?? 0;
                float factor = ranged?.CooldownReductionFactor ?? cooldown?.CooldownReductionFactor ?? 0;
                var cooldownSkill = item.m_shared.m_skillType;
                if (ranged == null && (cooldown == null || !MeleePresetCooldownSystem.TryResolveSkillType(cooldown.CooldownSkill, item, out cooldownSkill))) continue;
                if (cooldownSkill == skill && duration > 0 && factor > 0)
                    Add(EdgeText.Format("page_cooldown_reduction", EdgeText.Text(preset.NameKey),
                        (100 * MeleePresetCooldownSystem.ReadReduction(level, factor)).ToString("0.#"), Mathf.Clamp01(factor).ToString("0.##")));
            }
            var ambush = definition.SneakAmbush;
            if (skill == Skills.SkillType.Sneak && ambush != null && ambush.ChargeMaxSeconds > 0 && ambush.ChargeSkillFactor > 1)
                Add(EdgeText.Format("page_ambush_charge", EdgeText.Text("preset_sneak_ambush_name"), SneakAmbushChargeSystem.ReadChargeRate(level, ambush.ChargeSkillFactor).ToString("0.##")));
        }
        if (lines.Count > 0) lines.Insert(0, new SkillPageLine(Plugin.Name, heading: true));
        return lines;
    }
}

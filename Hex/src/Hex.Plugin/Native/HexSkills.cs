// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original native API implementation. VAT is a behavior only design reference; no source or assemblies accessed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Hex.Runtime.Native;

internal static class HexSkills
{
    internal readonly struct Row
    {
        internal readonly Skills.SkillType Type;
        internal readonly string Identity, Name;
        internal readonly float Level, Accumulator;
        internal readonly Sprite? Icon;
        internal Row(Skills.SkillDef definition, string identity, string name, Skills.Skill? progress)
        {
            Type = definition.m_skill; Identity = identity; Name = name; Icon = definition.m_icon;
            Level = progress?.m_level ?? 0f; Accumulator = progress?.m_accumulator ?? 0f;
        }
    }

    internal static void Initialize() => MeadworksMischief.Cheats.Tag(Plugin.Guid, "Skills.Edit", MischiefCategory.Progress);

    private static bool Allowed(out string message)
    {
        message = HexText.Text("skills_unavailable");
        if (!Plugin.Ready || !Plugin.Active || Plugin.Settings?.Enabled.Value != true
            || Plugin.Settings.SkillEditor.Value != true || !HexMovement.Owned(Player.m_localPlayer)) return false;
        if (MeadworksMischief.TryUse(Plugin.Guid, "Skills.Edit", MischiefCategory.Progress, HexText.Text("skills_label"))) return true;
        message = HexText.Text("actions_denied"); return false;
    }

    // Read definitions, not GetSkillList (trained only) or GetSkill (creates entries during a read).
    internal static bool Catalog(out IReadOnlyList<Row> rows, out string message)
    {
        rows = Array.Empty<Row>();
        if (!Allowed(out message)) return false;
        var native = Player.m_localPlayer.GetSkills();
        if (native == null || native.m_skills == null) return false;
        var custom = MeadworksSkills.All.Where(skill => skill.Registered).ToDictionary(skill => skill.Type);
        rows = native.m_skills.Concat(custom.Values.Select(skill => skill.GameDefinition))
            .Where(def => def != null && (int)def.m_skill > 0 && def.m_skill != Skills.SkillType.All)
            .GroupBy(def => def.m_skill).Select(group =>
            {
                var definition = group.First(); var type = definition.m_skill;
                var identity = custom.TryGetValue(type, out var skill) ? skill.Definition.Identity : type.ToString();
                var word = "$skill_" + type.ToString().ToLowerInvariant();
                var name = Localization.instance != null ? Localization.instance.Localize(word) : identity;
                if (name == word || string.IsNullOrWhiteSpace(name)) name = skill?.Definition.Name ?? identity;
                native.m_skillData.TryGetValue(type, out var progress);
                return new Row(definition, identity, name, progress);
            }).OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => (int)row.Type).ToArray();
        message = ""; return true;
    }

    internal static string List()
    {
        if (!Catalog(out var rows, out var message)) return message;
        return rows.Count == 0 ? HexText.Text("skills_empty") : string.Join("\n", rows.Select(row =>
            HexText.Format("skills_row", ((int)row.Type).ToString(CultureInfo.InvariantCulture), row.Identity, row.Name,
                row.Level.ToString("0.###", CultureInfo.InvariantCulture))));
    }

    internal static string Set(string identity, float level)
    {
        if (float.IsNaN(level) || float.IsInfinity(level) || level < 0f || level > 100f) return HexText.Text("skills_level_invalid");
        if (!Catalog(out var rows, out var message)) return message;
        var matches = rows.Where(row => row.Identity.Equals(identity, StringComparison.OrdinalIgnoreCase)
            || row.Type.ToString().Equals(identity, StringComparison.OrdinalIgnoreCase)
            || ((int)row.Type).ToString(CultureInfo.InvariantCulture) == identity).ToArray();
        if (matches.Length != 1) return HexText.Text("skills_unknown");
        var selected = matches[0];
        // Re-resolve at the write boundary; no cached actor or registry row can authorize a later mutation.
        if (!Allowed(out message)) return message;
        var native = Player.m_localPlayer.GetSkills();
        if (native == null || !native.m_skills.Any(def => def != null && def.m_skill == selected.Type))
            return HexText.Text("skills_unknown");
        var target = native.GetSkill(selected.Type);
        if (target == null || target.m_info == null) return HexText.Text("skills_unavailable");
        var progress = SkillMath.SetLevel(level);
        target.m_level = progress.Level; target.m_accumulator = progress.Accumulator;
        return HexText.Format("skills_set", selected.Name, progress.Level.ToString("0.###", CultureInfo.InvariantCulture));
    }
}

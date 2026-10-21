// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Effects;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Reads and checks one merged affix entry (format 2, classes-and-tiers.md section 3). Completeness is checked here,
    /// after all layers merged, so a two-line override of a built-in affix is valid while a new id missing a required
    /// field is an error. Whether the class ids exist is checked when both families are in (<see cref="ClassChecks"/>).
    /// </summary>
    internal static class AffixParser
    {
        private static readonly string[] Keys =
        {
            "id", "name", "effect", "param", "value", "unit", "affix", "family", "classes", "scaled", "requires", "category",
            "exclusion_group", "weight", "enabled", "hook", "tiers", RemovedSlots,
        };

        /// <summary>The format-1 key <c>classes</c> replaced: read nowhere, a warning where a user file still has it.</summary>
        private const string RemovedSlots = "slots";

        public static AffixDef? Parse(YamlNode node, RuleIssues issues)
        {
            if (!(node is YamlMappingNode map))
            {
                issues.Error("inscriptions", node, "an inscription entry should be a block of keyed values");
                return null;
            }
            string? id = new MapReader(map, "inscriptions[?]", issues).Id("id");
            if (id == null)
            {
                issues.Error("inscriptions[?]", map, "an inscription entry has no valid 'id'");
                return null;
            }
            MapReader r = new MapReader(map, $"inscriptions[{id}]", issues);
            r.Unknown(Keys);
            AffixDef def = new AffixDef { Id = id, Name = r.Str("name") ?? "$ecf_affix_" + id };
            ReadEffect(r, def);
            ReadPlacement(r, def);
            ReadDrawing(r, def);
            def.Tiers = AffixTierParser.Parse(r, def);
            return def;
        }

        private static void ReadEffect(MapReader r, AffixDef def)
        {
            def.Effect = Required(r, "effect") ?? "";
            def.Param = r.Str("param");
            def.Value = r.Enum("value", AffixValueType.Percent);
            Required(r, "value");
            def.Unit = r.Enum("unit", AffixUnit.None);
            if (def.Unit != AffixUnit.None && def.Value != AffixValueType.Flat)
            {
                r.Error("unit", "a unit is only for flat values");
            }
            CheckEffect(r, def);
        }

        private static void CheckEffect(MapReader r, AffixDef def)
        {
            if (def.Effect.Length == 0)
            {
                return;
            }
            if (!EffectRegistry.TryGet(def.Effect, out EffectDef effect))
            {
                r.Error("effect", $"'{def.Effect}' is not an effect this version implements");
                return;
            }
            def.EffectDef = effect;
            if (!effect.Accepts(def.Value))
            {
                r.Error("value", $"effect '{def.Effect}' does not take {EnumIds<AffixValueType>.Id(def.Value)} values");
            }
            string? paramError = ParamKinds.Apply(def, effect);
            if (paramError != null)
            {
                r.Error("param", paramError);
            }
        }

        private static void ReadPlacement(MapReader r, AffixDef def)
        {
            def.Kind = r.Enum("affix", AffixKind.Suffix);
            Required(r, "affix");
            def.Family = r.Id("family") ?? "";
            ReadClasses(r, def);
            def.Requires = RequirementsParser.Parse(r);
            def.Category = r.Enum("category", AffixCategory.Utility);
            Required(r, "category");
            if (r.Has(RemovedSlots))
            {
                r.Warn(RemovedSlots, "is no longer read: item classes replaced slots (classes: { best: [...], allowed: [...] })");
            }
        }

        // classes: { best: [...], allowed: [...] }; a class in both lists counts as best.
        private static void ReadClasses(MapReader r, AffixDef def)
        {
            MapReader? classes = r.Sub("classes");
            if (classes == null)
            {
                return;
            }
            classes.Value.Unknown("best", "allowed");
            List<string> best = ClassIds(classes.Value, "best");
            List<string> allowed = ClassIds(classes.Value, "allowed");
            if (allowed.RemoveAll(best.Contains) > 0)
            {
                classes.Value.Warn("allowed", "lists a class that is also in best; best wins");
            }
            def.BestClasses = best;
            def.AllowedClasses = allowed;
        }

        private static List<string> ClassIds(MapReader r, string key)
        {
            List<string> ids = new List<string>();
            foreach (string id in r.Strings(key) ?? new List<string>())
            {
                if (!Ids.IsValid(id))
                {
                    r.Error(key, $"'{id}' is not a valid class id");
                }
                else if (!ids.Contains(id))
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        private static void ReadDrawing(MapReader r, AffixDef def)
        {
            def.ExclusionGroup = r.Id("exclusion_group");
            def.Weight = r.Float("weight", 100f, 0f);
            def.Enabled = r.Bool("enabled", true);
            def.Hook = r.Enum("hook", HookDifficulty.None);
            def.Scaled = r.Bool("scaled", false);
            if (def.Scaled && def.Value == AffixValueType.Flag)
            {
                r.Warn("scaled", "a flag has no value to scale; ignored");
                def.Scaled = false;
            }
        }

        private static string? Required(MapReader r, string key)
        {
            string? value = r.Str(key);
            if (string.IsNullOrEmpty(value))
            {
                r.Issues.Error(r.At(key), r.Map, "is required");
                return null;
            }
            return value;
        }
    }
}

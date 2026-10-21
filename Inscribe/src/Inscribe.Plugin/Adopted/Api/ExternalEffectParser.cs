// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Rules;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// Reads an external effect's JSON (api.md section 3): <c>id</c> (required), <c>scope</c> (<c>player</c>, the
    /// default, or <c>item</c>), <c>value</c> (required: <c>percent</c>, <c>flat</c> or <c>flag</c>, or a list of them),
    /// <c>polarity</c> (<c>raise</c>, the default, or <c>lower</c>), <c>cap</c> (above 0; absent or null: uncapped),
    /// <c>param</c> (<c>none</c>, the default, or a param kind: <c>skill</c>, <c>damage_type</c>, <c>element</c>,
    /// <c>creature_family</c>, <c>resource</c>, <c>aura</c>) and an optional <c>description</c> (for listings).
    /// </summary>
    internal static class ExternalEffectParser
    {
        private static readonly string[] Keys = { "id", "scope", "value", "polarity", "cap", "param", "description" };

        public static EffectDef? Parse(MapReader r)
        {
            r.Unknown(Keys);
            string? id = r.Id("id");
            if (id == null)
            {
                r.Error("id", "an effect needs a valid 'id'");
                return null;
            }
            ValueTypes values = Values(r);
            EffectParamKind param = r.Enum("param", EffectParamKind.None);
            EffectPolarity polarity = r.Enum("polarity", EffectPolarity.Raise);
            string description = r.Str("description") ?? "external: applied by the mod that registered it";
            return new EffectDef(id, EffectRoute.Hook, Scope(r), values, param, polarity, Cap(r), HookDifficulty.None, 0, description);
        }

        private static EffectScope Scope(MapReader r)
        {
            string? text = r.Str("scope");
            switch (text)
            {
                case null:
                case "player":
                case "player_global":
                    return EffectScope.PlayerGlobal;
                case "item":
                case "item_local":
                    return EffectScope.ItemLocal;
                default:
                    r.Error("scope", $"'{text}' is not one of: player, item");
                    return EffectScope.PlayerGlobal;
            }
        }

        private static ValueTypes Values(MapReader r)
        {
            ValueTypes values = ValueTypes.None;
            foreach (string text in r.Strings("value") ?? new List<string>())
            {
                if (text != "none" && EnumIds<ValueTypes>.TryParse(text, out ValueTypes one))
                {
                    values |= one;
                }
                else
                {
                    r.Error("value", $"'{text}' is not one of: percent, flat, flag");
                }
            }
            if (values == ValueTypes.None && !r.Issues.HasErrors)
            {
                r.Error("value", "is required: percent, flat or flag (or a list of them)");
            }
            return values;
        }

        private static float? Cap(MapReader r)
        {
            if (!r.Has("cap") || YamlNodes.IsNull(r.Node("cap")))
            {
                return null;
            }
            float cap = r.Float("cap", 0f, 0f);
            if (cap <= 0f)
            {
                r.Error("cap", "a cap must be above 0 (leave it out, or null, for none)");
            }
            return cap;
        }
    }
}

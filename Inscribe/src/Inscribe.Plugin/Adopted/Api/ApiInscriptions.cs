// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Effects;
using ExtraPotions.Inscribe.Adopted.Rules;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Api
{
    /// <summary>
    /// Inscriptions, pools and external effects through the API (api.md section 3). An inscription's JSON must be a
    /// complete, valid entry on its own (read by the YAML's own <see cref="AffixParser"/>, its effect registered);
    /// it then joins the registered layer under the YAML (<see cref="CodeLayer"/>). A pool addition is applied after the
    /// merge. An external effect joins the effect registry (<see cref="EffectRegistry.RegisterExternal"/>). Each change
    /// schedules one rebuild of the inscription family at the end of the frame.
    /// </summary>
    internal static class ApiInscriptions
    {
        public static bool Register(string? json)
        {
            const string endpoint = "RegisterInscription";
            RuleIssues issues = new RuleIssues();
            YamlMappingNode? map = ApiJson.Map(json, endpoint, issues);
            if (map == null)
            {
                return false;
            }
            AffixDef? def = AffixParser.Parse(map, issues);
            if (!ApiJson.Accept(issues, endpoint) || def == null)
            {
                return false;
            }
            if (!CodeLayer.HasInscription(def.Id) && ActiveRules.Current.Affixes.Get(def.Id) != null)
            {
                Log.Warn($"API {endpoint}: '{def.Id}' is already an inscription of the YAML; the YAML's fields win over the registration (use an id of your own)");
            }
            CodeLayer.SetInscription(def.Id, map);
            RuleRebuild.Request(inscriptions: true);
            return true;
        }

        public static bool AddToPool(string? classId, string? inscriptionId, bool best)
        {
            if (!Ids.IsValid(classId) || !Ids.IsValid(inscriptionId))
            {
                Log.Warn($"API AddToPool: '{inscriptionId}' on '{classId}' refused (both must be valid ids)");
                return false;
            }
            if (CodeLayer.AddPool(inscriptionId!, classId!, best))
            {
                RuleRebuild.Request(inscriptions: true);
            }
            return true;
        }

        public static bool RegisterEffect(string? json)
        {
            const string endpoint = "RegisterExternalEffect";
            RuleIssues issues = new RuleIssues();
            YamlMappingNode? map = ApiJson.Map(json, endpoint, issues);
            if (map == null)
            {
                return false;
            }
            EffectDef? def = ExternalEffectParser.Parse(new MapReader(map, "effect", issues));
            if (!ApiJson.Accept(issues, endpoint) || def == null)
            {
                return false;
            }
            string? refused = EffectRegistry.RegisterExternal(def);
            if (refused != null)
            {
                Log.Warn($"API {endpoint}: {refused}; refused");
                return false;
            }
            RuleRebuild.Request(inscriptions: true);
            return true;
        }
    }
}

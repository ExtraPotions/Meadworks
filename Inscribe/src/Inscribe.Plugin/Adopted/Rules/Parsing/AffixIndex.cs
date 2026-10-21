// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Effects;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// The affix family's cross-entry work, done once per load: the id table, the channel table (one per effect and
    /// param of the enabled affixes, with its cap resolved per channel: see <see cref="ResolveCap"/>), the
    /// rollable pools per item class with their fit, and the load warnings (never-rolling affixes; unknown cap effects
    /// are errors).
    /// </summary>
    internal static class AffixIndex
    {
        public static void Build(AffixRules rules, RuleIssues issues)
        {
            Dictionary<string, AffixDef> byId = new Dictionary<string, AffixDef>(StringComparer.Ordinal);
            foreach (AffixDef def in rules.Affixes)
            {
                byId[def.Id] = def;
                WarnNeverRolls(def, issues);
            }
            rules.ById = byId;
            CheckCapKeys(rules, issues);
            rules.Channels = BuildChannels(rules);
            BuildPools(rules);
        }

        private static void WarnNeverRolls(AffixDef def, RuleIssues issues)
        {
            if (def.Enabled && (def.Weight <= 0f || NoTierWeight(def)))
            {
                issues.Warn($"inscriptions[{def.Id}]", null, "weight 0 (or every tier weight 0): it never rolls; existing copies keep working");
            }
            if (def.Enabled && def.BestClasses.Count == 0 && def.AllowedClasses.Count == 0)
            {
                issues.Warn($"inscriptions[{def.Id}].classes", null, "names no class in best or allowed: it never rolls");
            }
        }

        private static bool NoTierWeight(AffixDef def)
        {
            foreach (AffixTierDef tier in def.Tiers)
            {
                if (tier.Weight > 0f)
                {
                    return false;
                }
            }
            return true;
        }

        private static void CheckCapKeys(AffixRules rules, RuleIssues issues)
        {
            foreach (string key in rules.Caps.Keys)
            {
                string effect = EffectPart(key);
                if (!EffectRegistry.IsRegistered(effect))
                {
                    issues.Error($"caps.{key}", null, $"'{effect}' is not an effect this version implements");
                }
            }
        }

        private static string EffectPart(string key)
        {
            int cut = key.IndexOf(':');
            return cut < 0 ? key : key.Substring(0, cut);
        }

        public static string ChannelKey(AffixDef def) => def.Param == null ? def.Effect : def.Effect + ":" + def.Param;

        private static List<ChannelDef> BuildChannels(AffixRules rules)
        {
            List<ChannelDef> channels = new List<ChannelDef>();
            Dictionary<string, ChannelDef> byKey = new Dictionary<string, ChannelDef>(StringComparer.Ordinal);
            foreach (AffixDef def in rules.Affixes)
            {
                if (!def.Enabled)
                {
                    continue;
                }
                string key = ChannelKey(def);
                if (!byKey.TryGetValue(key, out ChannelDef channel))
                {
                    channel = NewChannel(rules, def, key, channels.Count);
                    byKey[key] = channel;
                    channels.Add(channel);
                }
                def.ChannelIndex = channel.Index;
            }
            return channels;
        }

        private static ChannelDef NewChannel(AffixRules rules, AffixDef def, string key, int index)
        {
            return new ChannelDef
            {
                Index = index,
                Key = key,
                Effect = def.EffectDef,
                Param = def.Param,
                Cap = ResolveCap(rules, def, key),
                Sample = def,
            };
        }

        /// <summary>
        /// The cap of one channel (classes-and-tiers.md section 8): the most specific <c>caps:</c> key wins, each
        /// channel capped on its own. <c>effect:param</c>, then <c>effect</c> (so <c>damage_taken: 50</c> caps every
        /// damage type separately), then the registry default.
        /// </summary>
        private static float ResolveCap(AffixRules rules, AffixDef def, string key)
        {
            if (rules.Caps.TryGetValue(key, out float cap) || rules.Caps.TryGetValue(def.Effect, out cap))
            {
                return cap;
            }
            return def.EffectDef.DefaultCap ?? float.PositiveInfinity;
        }

        // One pool per class id any affix names, in file order; unknown ids get a pool no item reaches (ClassChecks warns).
        private static void BuildPools(AffixRules rules)
        {
            Dictionary<string, List<PoolEntry>> pools = new Dictionary<string, List<PoolEntry>>(StringComparer.Ordinal);
            foreach (AffixDef def in rules.Affixes)
            {
                if (!def.Enabled || def.Weight <= 0f)
                {
                    continue;
                }
                AddAll(pools, def, def.BestClasses, ClassFit.Best);
                AddAll(pools, def, def.AllowedClasses, ClassFit.Allowed);
            }
            rules.Pools = Freeze(pools);
        }

        private static void AddAll(Dictionary<string, List<PoolEntry>> pools, AffixDef def, IReadOnlyList<string> classes, ClassFit fit)
        {
            foreach (string classId in classes)
            {
                if (!pools.TryGetValue(classId, out List<PoolEntry> list))
                {
                    list = new List<PoolEntry>();
                    pools[classId] = list;
                }
                list.Add(new PoolEntry(def, fit));
            }
        }

        private static Dictionary<string, PoolEntry[]> Freeze(Dictionary<string, List<PoolEntry>> pools)
        {
            Dictionary<string, PoolEntry[]> frozen = new Dictionary<string, PoolEntry[]>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<PoolEntry>> pair in pools)
            {
                frozen[pair.Key] = pair.Value.ToArray();
            }
            return frozen;
        }
    }
}

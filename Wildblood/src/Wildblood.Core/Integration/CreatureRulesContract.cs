// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ExtraPotions.Wildblood.Traits;

namespace ExtraPotions.Wildblood.Integration
{
    /// <summary>Optional creature defaults. Each accepted registration replaces that prefab's entire declaration.</summary>
    public static class CreatureRulesContract
    {
        public const string Id = "meadworks.creatures.rules";
        public static readonly Version Version = new Version(1, 0);
        public const int MaximumBytes = 65536;
        public const int MaximumEntries = 256;

        public static bool TryRead(string prefab, string json, out CreatureRule rule)
        {
            rule = null!;
            if (!ValidName(prefab) || json == null || json.Length > 4096) return false;
            try
            {
                var dto = Read<CreatureRule>(json);
                if (dto == null || dto.Prefab != prefab || !Valid(dto)) return false;
                rule = dto;
                return true;
            }
            catch (Exception) { return false; }
        }

        public static string Snapshot(IEnumerable<CreatureRule> rules) => Write(new Bundle
        {
            Version = 1, Rules = rules.OrderBy(r => r.Prefab, StringComparer.Ordinal).ToArray(),
        });

        public static bool TrySnapshot(string json, out IReadOnlyDictionary<string, CreatureRule> rules)
        {
            rules = new Dictionary<string, CreatureRule>(StringComparer.Ordinal);
            if (json == null || json.Length > MaximumBytes || Encoding.UTF8.GetByteCount(json) > MaximumBytes) return false;
            try
            {
                var bundle = Read<Bundle>(json);
                if (bundle == null || bundle.Version != 1 || bundle.Rules == null || bundle.Rules.Length > MaximumEntries) return false;
                var result = new Dictionary<string, CreatureRule>(StringComparer.Ordinal);
                foreach (var rule in bundle.Rules)
                {
                    if (rule == null || !Valid(rule) || result.ContainsKey(rule.Prefab)) return false;
                    result.Add(rule.Prefab, rule);
                }
                rules = result;
                return true;
            }
            catch (Exception) { return false; }
        }

        static bool Valid(CreatureRule rule) => ValidName(rule.Prefab)
            && (rule.Body == null || rule.Body == "large" || rule.Body == "small")
            && (!rule.CloakedReveal.HasValue || rule.CloakedReveal >= 0 && rule.CloakedReveal <= 60)
            && Names(rule.NeverTraits, name => Mutations.TryParseId(name, out _))
            && Names(rule.ExcludedAspects, name => Enum.TryParse(name, true, out Bosses.Aspect aspect)
                && Enum.IsDefined(typeof(Bosses.Aspect), aspect) && aspect != Bosses.Aspect.None
                && string.Equals(name, aspect.ToString(), StringComparison.OrdinalIgnoreCase));

        static bool Names(string[]? names, Func<string, bool> valid) => names == null || names.Length <= 32
            && names.All(name => !string.IsNullOrEmpty(name) && name.Length <= 32 && valid(name));
        static bool ValidName(string name) => !string.IsNullOrEmpty(name) && name.Length <= 128
            && name.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.');

        internal static T? Read<T>(string json) where T : class
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return new DataContractJsonSerializer(typeof(T)).ReadObject(stream) as T;
        }
        internal static string Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        [DataContract]
        sealed class Bundle
        {
            [DataMember(Name = "version", IsRequired = true)] public int Version;
            [DataMember(Name = "rules", IsRequired = true)] public CreatureRule[] Rules = Array.Empty<CreatureRule>();
        }
    }

    [DataContract]
    public sealed class CreatureRule
    {
        [DataMember(Name = "prefab", IsRequired = true)] public string Prefab { get; private set; } = "";
        [DataMember(Name = "boss", EmitDefaultValue = false)] public bool Boss { get; private set; }
        [DataMember(Name = "mini_boss", EmitDefaultValue = false)] public bool MiniBoss { get; private set; }
        [DataMember(Name = "worthless", EmitDefaultValue = false)] public bool Worthless { get; private set; }
        [DataMember(Name = "body", EmitDefaultValue = false)] public string? Body { get; private set; }
        [DataMember(Name = "cloaked_reveal", EmitDefaultValue = false)] public int? CloakedReveal { get; private set; }
        [DataMember(Name = "never_traits", EmitDefaultValue = false)] internal string[]? NeverTraits;
        [DataMember(Name = "never_devoured", EmitDefaultValue = false)] public bool NeverDevoured { get; private set; }
        [DataMember(Name = "excluded_aspects", EmitDefaultValue = false)] internal string[]? ExcludedAspects;
        public int BarredTraits => NeverTraits == null ? 0 : NeverTraits.Aggregate(0, (mask, name) =>
            Mutations.TryParseId(name, out var mutation) ? mask | Mutations.Bit(mutation) : mask);
        public bool Excludes(string aspect) => ExcludedAspects != null
            && ExcludedAspects.Any(name => string.Equals(name, aspect, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class CreatureRuleRegistry
    {
        readonly Dictionary<string, CreatureRule> local = new Dictionary<string, CreatureRule>(StringComparer.Ordinal);
        public string Snapshot { get; private set; } = CreatureRulesContract.Snapshot(Array.Empty<CreatureRule>());
        public bool Register(string prefab, string json)
        {
            if (!CreatureRulesContract.TryRead(prefab, json, out var rule)
                || !local.ContainsKey(prefab) && local.Count >= CreatureRulesContract.MaximumEntries) return false;
            var candidate = CreatureRulesContract.Snapshot(local.Values.Where(r => r.Prefab != prefab).Concat(new[] { rule }));
            if (Encoding.UTF8.GetByteCount(candidate) > CreatureRulesContract.MaximumBytes) return false;
            local[prefab] = rule;
            Snapshot = candidate;
            return true;
        }
    }
}

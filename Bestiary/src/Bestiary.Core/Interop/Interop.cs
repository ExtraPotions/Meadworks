// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ExtraPotions.Bestiary.Config;
using ExtraPotions.Bestiary.Spawning;

namespace ExtraPotions.Bestiary.Interop
{
    /// <summary>
    /// Crucible capability ids Bestiary provides or consumes (design 7, 9). Contracts are BCL delegate types so no
    /// product assembly references another; absence of a provider is the standalone behavior.
    /// <list type="bullet">
    /// <item><see cref="CreatureRules"/> v1 (Wildblood provides): <c>Func&lt;string prefab, string json, bool&gt;</c> RegisterDefaults.</item>
    /// <item><see cref="Disguise"/> v1 (Bestiary provides): <c>Func&lt;object zdo, bool&gt;</c> IsDisguised.</item>
    /// <item><see cref="ItemClasses"/> v1 (Inscribe provides): <c>Tuple&lt;Func&lt;string cls, string prefab, bool&gt; ClaimItems,
    /// Func&lt;string prefab, int level, bool&gt; SetItemLevel, Func&lt;string json, bool&gt; SetCreatureLoot&gt;</c>.</item>
    /// <item><see cref="BestiaryWorld"/> v1 (Bestiary provides, Delve consumes): <c>Func&lt;int&gt;</c> ResetMountainClaims, the Rime Giant and Slag Charred region claims cleared. Marrow Lanterns have no claim.</item>
    /// </list>
    /// Inscribe publishes ItemClasses in this shape; the CreatureRules shape is Bestiary's proposal until Wildblood publishes one.
    /// </summary>
    public static class CapabilityIds
    {
        public const string CreatureRules = "meadworks.creatures.rules";
        public const string Disguise = "meadworks.creatures.disguise";
        public const string ItemClasses = "meadworks.items.classes";
        public const string BestiaryWorld = "meadworks.bestiary.world";

        public static readonly Version Version1 = new Version(1, 0);

        public static IReadOnlyList<string> All { get; } = new[] { CreatureRules, Disguise, ItemClasses, BestiaryWorld };
    }

    /// <summary>One creature's defaults for Wildblood (design 9.1).</summary>
    public sealed class CreatureDefaults
    {
        public CreatureDefaults(string prefab, bool boss = false, bool miniBoss = false, bool worthless = false, string? body = null,
            int cloakedReveal = 0, IReadOnlyList<string>? neverTraits = null, bool neverDevoured = false, IReadOnlyList<string>? excludedAspects = null)
        {
            Prefab = prefab;
            Boss = boss;
            MiniBoss = miniBoss;
            Worthless = worthless;
            Body = body;
            CloakedReveal = cloakedReveal;
            NeverTraits = neverTraits ?? Array.Empty<string>();
            NeverDevoured = neverDevoured;
            ExcludedAspects = excludedAspects ?? Array.Empty<string>();
        }

        public string Prefab { get; }
        public bool Boss { get; }
        public bool MiniBoss { get; }
        public bool Worthless { get; }
        public string? Body { get; }
        public int CloakedReveal { get; }
        public IReadOnlyList<string> NeverTraits { get; }
        public bool NeverDevoured { get; }
        public IReadOnlyList<string> ExcludedAspects { get; }
    }

    /// <summary>Design 9.1: what Bestiary registers through <c>meadworks.creatures.rules</c> v1 when Wildblood runs.</summary>
    public static class WildbloodDefaults
    {
        public static IReadOnlyList<CreatureDefaults> All { get; } = new[]
        {
            new CreatureDefaults("ECP_Kraken", boss: true, excludedAspects: new[] { "Twin", "Phantom", "Tethered", "Brutal" }),
            new CreatureDefaults("ECP_RimeGiant", body: "large", cloakedReveal: 15, neverTraits: new[] { "Gilded", "Relentless" }, neverDevoured: true),
            new CreatureDefaults("ECP_Headsman", miniBoss: true),
            new CreatureDefaults("ECP_HeadsmanSkeleton", worthless: true),
            new CreatureDefaults(KitCreatures.BogHook),
            new CreatureDefaults(KitCreatures.Javelineer),
            new CreatureDefaults(KitCreatures.SlagCharred),
            new CreatureDefaults(KitCreatures.MarrowLantern, cloakedReveal: 12),
        };

        public static CreatureDefaults? Find(string prefab) => All.FirstOrDefault(d => d.Prefab == prefab);

        /// <summary>Compact JSON in a fixed field order. Unset optional fields are left out. Lantern Reveal 0 is written: omitting cloaked_reveal clears the rule.</summary>
        public static string Json(CreatureDefaults d, int marrowReveal = 12)
        {
            var reveal = d.Prefab == KitCreatures.MarrowLantern ? marrowReveal : d.CloakedReveal;
            var b = new StringBuilder("{\"prefab\":").Append(Quote(d.Prefab));
            if (d.Boss) b.Append(",\"boss\":true");
            if (d.MiniBoss) b.Append(",\"mini_boss\":true");
            if (d.Worthless) b.Append(",\"worthless\":true");
            if (d.Body != null) b.Append(",\"body\":").Append(Quote(d.Body));
            // 0 on any other prefab means the field was never set. Writing it would replace Wildblood's ordinary distance.
            if (reveal > 0 || d.Prefab == KitCreatures.MarrowLantern && reveal == 0)
                b.Append(",\"cloaked_reveal\":").Append(reveal);
            if (d.NeverTraits.Count > 0) b.Append(",\"never_traits\":[").Append(string.Join(",", d.NeverTraits.Select(Quote))).Append(']');
            if (d.NeverDevoured) b.Append(",\"never_devoured\":true");
            if (d.ExcludedAspects.Count > 0) b.Append(",\"excluded_aspects\":[").Append(string.Join(",", d.ExcludedAspects.Select(Quote))).Append(']');
            return b.Append('}').ToString();
        }

        private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    /// <summary>Design 9.1: a dormant mimic is disguised (<c>ecp_disguised</c>); Wildblood holds back its look until it wakes.</summary>
    public static class Disguise
    {
        public static bool IsDisguised(string prefab, bool disguisedFlag) => prefab == "ECP_CryptMimic" && disguisedFlag;
    }

    public enum InteropOutcome
    {
        /// <summary>No provider: Bestiary behaves standalone.</summary>
        Absent,

        Registered,

        /// <summary>The provider refused some entries (logged at Verbose by the plugin).</summary>
        Partial,

        /// <summary>The provider threw; Bestiary carries on standalone.</summary>
        Failed,
    }

    /// <summary>Sends the tables to a resolved provider; a missing provider is <see cref="InteropOutcome.Absent"/>, a throwing one never escapes.</summary>
    public static class InteropCalls
    {
        public static InteropOutcome RegisterDefaults(Func<string, string, bool>? register, int marrowReveal = 12)
        {
            if (register == null) return InteropOutcome.Absent;
            return Send(WildbloodDefaults.All.Select(d => (Func<bool>)(() => register(d.Prefab, WildbloodDefaults.Json(d, marrowReveal)))));
        }

        public static InteropOutcome RegisterInscribe(Tuple<Func<string, string, bool>, Func<string, int, bool>, Func<string, bool>>? classes,
            BiomeMask rimeGiantBiomes, BiomeMask slingerBiomes)
        {
            if (classes == null || classes.Item1 == null || classes.Item2 == null || classes.Item3 == null) return InteropOutcome.Absent;
            var calls = InscribeTable.Gear.Select(g => (Func<bool>)(() => classes.Item1(g.ItemClass, g.Prefab) & classes.Item2(g.Prefab, g.Level)))
                .Concat(InscribeTable.CreatureLoot(rimeGiantBiomes, slingerBiomes).Select(p => (Func<bool>)(() => classes.Item3(p.Json))));
            return Send(calls);
        }

        private static InteropOutcome Send(IEnumerable<Func<bool>> calls)
        {
            var refused = 0;
            try
            {
                foreach (var call in calls)
                    if (!call()) refused++;
            }
            catch (Exception)
            {
                return InteropOutcome.Failed;
            }
            return refused == 0 ? InteropOutcome.Registered : InteropOutcome.Partial;
        }
    }
}

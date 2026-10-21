// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
using System.Collections.Generic;
using ExtraPotions.Kindle.Runtime.Salvage;
using ExtraPotions.Kindle.Runtime.Salvage;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// The model of Kindle.Salvage*.yml: <c>deny:</c> (a list in the item vocabulary, never salvageable),
    /// <c>overrides:</c> (vocabulary key to <c>{ fraction: x }</c> or a bare number) and <c>groups:</c>. Matchers
    /// are parsed in Verify so the groups of every file of the set are known.
    /// </summary>
    public sealed class SalvageModel : YamlModel
    {
        private static bool excludeWarned;
        private readonly List<string> denyEntries = new List<string>();

        public ItemGroups ItemGroups { get; } = new ItemGroups();

        public ItemMatchSet Deny { get; private set; } = ItemMatchSet.Empty;

        public List<FractionOverride> Overrides { get; } = new List<FractionOverride>();

        protected override void Read(YamlNode root)
        {
            YamlNode groups = root.Get("groups");
            if (groups.Kind == YamlNodeKind.Map)
                ItemGroups.Read(groups);
            YamlNode deny = root.Get("deny");
            if (IsPresent(deny) && deny.TryStringList(out List<string> entries))
                denyEntries.AddRange(entries);
            YamlNode overrides = root.Get("overrides");
            if (overrides.Kind == YamlNodeKind.Map)
                foreach (KeyValuePair<string, YamlNode> entry in overrides.Entries)
                    ReadOverride(entry.Key, entry.Value);
            WarnRenamedExclude(root);
        }

        /// <summary>
        /// A 1.0.0 file says <c>exclude:</c>, renamed <c>deny</c> in 1.1.0. Asking for the key keeps the generic
        /// unknown-key warning away; one plain warning names the rename instead, once per session (the set is
        /// built at startup and again when the server's copy arrives). The list itself stays ignored.
        /// </summary>
        private static void WarnRenamedExclude(YamlNode root)
        {
            if (excludeWarned || !IsPresent(root.Get("exclude")))
                return;
            excludeWarned = true;
            root.Warn("the key exclude was renamed deny in 1.1.0; rename it in your file");
        }

        protected override void Verify()
        {
            Deny = ItemMatchSet.Parse(denyEntries, ItemGroups);
            foreach (FractionOverride entry in Overrides)
                entry.Matcher = ItemMatcher.Parse(entry.Key, ItemGroups);
        }

        private void ReadOverride(string key, YamlNode value)
        {
            YamlNode number = value.Kind == YamlNodeKind.Scalar ? value : value.Get("fraction");
            if (!number.TryFloat(out float fraction))
            {
                if (number.Kind == YamlNodeKind.Missing)
                    value.Error("an override needs a fraction, for example { fraction: 1.0 }");
                return;
            }
            if (fraction < 0f || float.IsNaN(fraction) || float.IsInfinity(fraction))
            {
                number.Error("the fraction cannot be negative");
                return;
            }
            Overrides.Add(new FractionOverride(key, fraction));
        }

        private static bool IsPresent(YamlNode node) => node.Kind != YamlNodeKind.Missing && node.Kind != YamlNodeKind.Null;
    }
}

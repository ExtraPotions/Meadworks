// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
using ExtraPotions.Kindle.Runtime.Salvage;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>One entry of the YAML <c>overrides:</c> map: a vocabulary key and the return fraction for the items it matches.</summary>
    public sealed class FractionOverride
    {
        public FractionOverride(string key, float fraction)
        {
            Key = key;
            Fraction = fraction;
        }

        public string Key { get; }

        public float Fraction { get; }

        /// <summary>Parsed once every group of every file is known (in the model's Verify).</summary>
        public ItemMatcher Matcher { get; set; }

        public bool IsExact => Matcher != null && Matcher.IsExactName;

        public bool Matches(ItemDrop.ItemData item) => Matcher != null && Matcher.Matches(item);
    }
}

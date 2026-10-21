// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Affixes
{
    /// <summary>
    /// Writes <see cref="StateData"/> into a custom-data dictionary (item-data.md section 3): only our keys, a key
    /// whose value would be empty is removed, <c>ecf_v</c> present exactly when another of our keys is, the reserved
    /// <c>ecf_tier</c> left as it was, the retired keys removed, and every other mod's key untouched.
    /// </summary>
    internal static class ItemWriter
    {
        public static void Serialize(StateData data, Dictionary<string, string> target)
        {
            Set(target, ItemKeys.Rarity, data.RarityId);
            Set(target, ItemKeys.Affixes, ItemCodec.EncodeList(data.Segments));
            target.Remove(ItemKeys.LegacyAffixes);
            Set(target, ItemKeys.Sealed, data.SealedReason);
            Set(target, ItemKeys.Sockets, GemCodec.EncodeSockets(data.Sockets));
            Set(target, ItemKeys.Gems, GemCodec.EncodeGems(data.Gems));
            foreach (string key in ItemKeys.RetiredKeys)
            {
                target.Remove(key);
            }
            Set(target, ItemKeys.Version, AnyStateKey(target) ? Numbers.Format(ItemKeys.CurrentFormat) : null);
        }

        private static bool AnyStateKey(Dictionary<string, string> target)
        {
            foreach (string key in ItemKeys.StateKeys)
            {
                if (target.ContainsKey(key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Sets a key, or removes it when the value is empty.</summary>
        internal static void Set(Dictionary<string, string> target, string key, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                target.Remove(key);
            }
            else
            {
                target[key] = value!;
            }
        }
    }
}

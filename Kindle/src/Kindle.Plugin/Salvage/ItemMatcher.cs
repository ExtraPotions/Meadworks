// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
using System;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// One entry of the item vocabulary: <c>Wood</c> (prefab name, case-insensitive), <c>$item_wood</c> (shared
    /// name token), <c>prefix:Trophy</c>, <c>suffix:Ore</c>, <c>type:Material</c> (an ItemDrop.ItemData.ItemType),
    /// <c>group:Ores</c> (a group of the file's groups map) and <c>*</c> (everything). Parsing never throws; an
    /// entry that cannot be understood matches nothing and says why in <see cref="Problem"/>.
    /// </summary>
    public sealed class ItemMatcher
    {
        private enum Kind { Nothing, Everything, Name, Token, Prefix, Suffix, Type, Group }

        private readonly Kind kind;
        private readonly string text;
        private readonly string source;
        private readonly ItemDrop.ItemData.ItemType type;
        private readonly ItemGroups groups;

        private ItemMatcher(Kind kind, string text, string source, ItemGroups groups, string problem, ItemDrop.ItemData.ItemType type = ItemDrop.ItemData.ItemType.None)
        {
            this.kind = kind;
            this.text = text;
            this.source = source;
            this.groups = groups ?? ItemGroups.Empty;
            this.type = type;
            Problem = problem;
        }

        /// <summary>Why the entry matches nothing, or null when it is fine.</summary>
        public string Problem { get; }

        /// <summary>A plain prefab or $token name: specific wins over patterns.</summary>
        public bool IsExactName => kind == Kind.Name || kind == Kind.Token;

        /// <summary>The group name of a <c>group:</c> entry, else null. Lets <see cref="ItemGroups"/> recurse cycle safe.</summary>
        public string GroupName => kind == Kind.Group ? text : null;

        public static ItemMatcher Parse(string text, ItemGroups groups)
        {
            string trimmed = (text ?? "").Trim();
            if (trimmed.Length == 0)
                return new ItemMatcher(Kind.Nothing, "", "", groups, "empty entry");
            if (trimmed == "*")
                return new ItemMatcher(Kind.Everything, "*", "*", groups, null);
            int colon = trimmed.IndexOf(':');
            if (colon > 0)
                return ParsePattern(trimmed.Substring(0, colon).Trim().ToLowerInvariant(), trimmed.Substring(colon + 1).Trim(), trimmed, groups);
            return new ItemMatcher(trimmed.StartsWith("$") ? Kind.Token : Kind.Name, trimmed, trimmed, groups, null);
        }

        private static ItemMatcher ParsePattern(string keyword, string value, string source, ItemGroups groups)
        {
            if (value.Length == 0)
                return new ItemMatcher(Kind.Nothing, "", source, groups, "nothing after '" + keyword + ":'");
            switch (keyword)
            {
                case "prefix": return new ItemMatcher(Kind.Prefix, value, source, groups, null);
                case "suffix": return new ItemMatcher(Kind.Suffix, value, source, groups, null);
                case "group": return new ItemMatcher(Kind.Group, value, source, groups, null);
                case "type": return ParseType(value, source, groups);
                default: return new ItemMatcher(Kind.Nothing, "", source, groups, "unknown matcher '" + keyword + ":'; use prefix:, suffix:, type: or group:");
            }
        }

        private static ItemMatcher ParseType(string value, string source, ItemGroups groups)
        {
            if (Enum.TryParse(value, true, out ItemDrop.ItemData.ItemType parsed))
                return new ItemMatcher(Kind.Type, parsed.ToString(), source, groups, null, parsed);
            return new ItemMatcher(Kind.Nothing, "", source, groups, "unknown item type '" + value + "'; known: " + string.Join(", ", Enum.GetNames(typeof(ItemDrop.ItemData.ItemType))));
        }

        public bool Matches(ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null && Matches(ItemNames.PrefabName(item), item.m_shared);
        }

        public bool Matches(string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            switch (kind)
            {
                case Kind.Everything: return true;
                case Kind.Name: return string.Equals(prefabName, text, StringComparison.OrdinalIgnoreCase);
                case Kind.Token: return shared != null && string.Equals(shared.m_name, text, StringComparison.OrdinalIgnoreCase);
                case Kind.Prefix: return prefabName != null && prefabName.StartsWith(text, StringComparison.OrdinalIgnoreCase);
                case Kind.Suffix: return prefabName != null && prefabName.EndsWith(text, StringComparison.OrdinalIgnoreCase);
                case Kind.Type: return shared != null && shared.m_itemType == type;
                case Kind.Group: return groups.Matches(text, prefabName, shared);
                default: return false;
            }
        }

        public override string ToString() => source;
    }
}

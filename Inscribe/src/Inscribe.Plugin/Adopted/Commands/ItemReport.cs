// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// The <c>inspect</c> block for one item (console-commands.md section 3): headline, the raw <c>ecf_</c> keys, the
    /// rarity and format, one line per inscription with its effect and whether it is active, the unreadable segments,
    /// the socketed gems and the socket count, then the seal. The raw line is what two players compare in the multiplayer checklist, so it
    /// prints the stored strings exactly, in a fixed key order.
    /// </summary>
    internal static class ItemReport
    {
        public static void Write(CommandCall call, ItemDrop.ItemData item, string? where = null)
        {
            ItemState state = ItemState.Read(item);
            call.Reply(Headline(item) + (where != null ? $", from {where}" : ""));
            call.Detail("raw: " + Raw(item.m_customData));
            call.Detail(RarityLine(state));
            for (int i = 0; i < state.AffixCount; i++)
            {
                call.Detail(AffixLine(state, i));
            }
            foreach (string raw in state.Unreadable)
            {
                call.Detail($"unreadable segment '{raw}' (kept as it is)");
            }
            for (int i = 0; i < state.Gems.Count; i++)
            {
                call.Detail(GemLine(state, i));
            }
            call.Detail($"sockets {state.Sockets}, {state.FilledSockets} filled");
            call.Detail(state.IsSealed ? $"sealed ({state.SealedReason})" : "not sealed");
        }

        private static string Headline(ItemDrop.ItemData item)
        {
            string itemClass = ItemClasses.Classify(item).ClassId ?? "-";
            string kind = ItemClasses.IsMagicBase(item) ? "magic base" : ItemClasses.IsStone(item) ? "a rune, not a magic base" : "not a magic base";
            return $"{ItemText.Name(item)} [{item.m_quality}] ({ItemText.Prefab(item)}), class {itemClass}, "
                + $"item level {ItemTier.Of(item)}, {kind}";
        }

        /// <summary>Every <c>ecf_</c> key as stored: the version, the state keys in order, then any other <c>ecf_</c> key.</summary>
        public static string Raw(Dictionary<string, string>? data)
        {
            if (data == null || data.Count == 0)
            {
                return "(no custom data)";
            }
            StringBuilder sb = new StringBuilder();
            Append(sb, data, ItemKeys.Version);
            foreach (string key in ItemKeys.StateKeys)
            {
                Append(sb, data, key);
            }
            AppendOthers(sb, data, out int foreign);
            string text = sb.Length == 0 ? "(no ecf_ data)" : sb.ToString();
            return foreign > 0 ? $"{text} (+{foreign} keys of other mods)" : text;
        }

        private static void Append(StringBuilder sb, Dictionary<string, string> data, string key)
        {
            if (data.TryGetValue(key, out string value))
            {
                sb.Append(sb.Length > 0 ? " | " : "").Append(key).Append('=').Append(value);
            }
        }

        private static void AppendOthers(StringBuilder sb, Dictionary<string, string> data, out int foreign)
        {
            foreign = 0;
            List<string> extra = new List<string>();
            foreach (string key in data.Keys)
            {
                bool ours = key.StartsWith(ItemKeys.Prefix, System.StringComparison.Ordinal);
                bool known = key == ItemKeys.Version || System.Array.IndexOf(ItemKeys.StateKeys, key) >= 0;
                foreign += ours ? 0 : 1;
                if (ours && !known)
                {
                    extra.Add(key);
                }
            }
            extra.Sort(System.StringComparer.Ordinal);
            extra.ForEach(key => Append(sb, data, key));
        }

        private static string RarityLine(ItemState state)
        {
            string rarity = !state.IsMagic ? "rarity normal"
                : state.Rarity == null ? $"rarity {state.RarityId} (unknown: not in the configuration)"
                : $"rarity {state.RarityId} ({state.Rarity.Color}), {state.Rarity.MinAffixes}-{state.Rarity.MaxAffixes} inscriptions";
            string format = state.IsEmpty ? "no format key" : $"format v{state.Format}";
            return state.IsNewerFormat ? $"{rarity}, {format} (newer than this version: runes refuse)" : $"{rarity}, {format}";
        }

        private static string AffixLine(ItemState state, int index)
        {
            AffixRoll roll = state.Affixes[index];
            AffixDef? def = state.DefinitionAt(index);
            if (def == null)
            {
                return $"inscription {roll.Id} grade {roll.Tier} {Numbers.Format(roll.Value)} -> dormant (not in configuration)";
            }
            string head = $"inscription {roll.Id} T{def.ShownTier(roll.Tier)} of {def.TierCount} {Numbers.Format(roll.Value)} "
                + $"({EnumIds<AffixKind>.Id(def.Kind)}) -> ";
            string effect = def.Param == null ? def.Effect : $"{def.Effect}:{def.Param}";
            string status = def.Enabled ? "active" : "dormant (disabled)";
            string tier = def.TierRow(roll.Tier) == null ? ", tier not defined any more" : "";
            return $"{head}effect {effect}, {status}{tier}";
        }

        private static string GemLine(ItemState state, int index)
        {
            GemRoll gem = state.Gems[index];
            AffixDef? def = state.GemDefinitionAt(index);
            string head = $"socket {state.GemSocketAt(index) + 1}: {gem.GemId} gives {gem.Roll.Id}";
            if (def == null)
            {
                return $"{head} grade {gem.Roll.Tier} {Numbers.Format(gem.Roll.Value)} -> dormant (not in configuration)";
            }
            string status = def.Enabled ? "active" : "dormant (disabled)";
            return $"{head} T{def.ShownTier(gem.Roll.Tier)} of {def.TierCount} {Numbers.Format(gem.Roll.Value)} -> {status}";
        }
    }

    /// <summary>Names of an item for console output.</summary>
    internal static class ItemText
    {
        /// <summary>The display name in the player's language (the raw token before the game is up).</summary>
        public static string Name(ItemDrop.ItemData item) => Words.Localize(item.m_shared?.m_name ?? "?");

        public static string Prefab(ItemDrop.ItemData item) => ItemTier.PrefabName(item) ?? "?";
    }
}

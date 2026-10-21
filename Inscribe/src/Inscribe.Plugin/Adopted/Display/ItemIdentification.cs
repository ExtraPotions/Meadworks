// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Runtime.CompilerServices;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Runtime;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    internal static class ItemIdentification
    {
        private sealed class Entry
        {
            public ItemState? State;
            public int Generation = -1, Quality, MaxQuality;
            public long TranslationRevision = -1;
            public bool RarityDisplay;
            public string? Language, NameToken;
            public string Summary = "";
            public string? Name, IdentifiedName;
            public bool HasName;
        }
        private static readonly ConditionalWeakTable<ItemDrop.ItemData, Entry> Summaries = new ConditionalWeakTable<ItemDrop.ItemData, Entry>();


        internal static string Summary(ItemDrop.ItemData item) => SummaryForQuality(item, item?.m_quality ?? 0);

        internal static string SummaryForQuality(ItemDrop.ItemData item, int quality)
        {
            if (item?.m_shared == null || !InscribeModules.Active(InscribeModule.Display)) return "";
            bool rarityDisplay = InscribeModules.StepAside.RarityDisplay;
            ItemState state = rarityDisplay ? ItemState.Read(item) : ItemState.Empty;
            int maxQuality = item.m_shared.m_maxQuality;
            string? language = global::Localization.instance?.GetSelectedLanguage();
            Entry entry = Summaries.GetOrCreateValue(item);
            if (ReferenceEquals(entry.State, state) && entry.Generation == DisplayCache.Generation && entry.Quality == quality
                && entry.MaxQuality == maxQuality && entry.RarityDisplay == rarityDisplay
                && entry.Language == language && entry.NameToken == item.m_shared.m_name && entry.TranslationRevision == InscribeText.Revision)
                return entry.Summary;
            entry.State = state; entry.Generation = DisplayCache.Generation; entry.Quality = quality;
            entry.MaxQuality = maxQuality; entry.RarityDisplay = rarityDisplay; entry.Language = language;
            entry.NameToken = item.m_shared.m_name;
            entry.TranslationRevision = InscribeText.Revision;
            entry.Name = entry.IdentifiedName = null; entry.HasName = false;
            var parts = new StringBuilder();
            if (!state.IsEmpty || maxQuality > 1 || quality > 1)
            {
                if (rarityDisplay) parts.Append(Rarity(state));
                if (maxQuality > 1 || quality > 1) Part(parts, InscribeText.Format("tooltip_quality", quality));
                int inscriptions = state.AffixCount + state.Unreadable.Count;
                if (inscriptions > 0 || state.Sockets > 0)
                {
                    parts.Append('\n');
                    if (inscriptions > 0) parts.Append(InscribeText.Format("tooltip_inscriptions", inscriptions));
                    if (state.Sockets > 0) Part(parts, InscribeText.Format("tooltip_sockets", state.FilledSockets, state.Sockets));
                }
            }
            return entry.Summary = parts.ToString();
        }

        private static void Part(StringBuilder text, string part)
        {
            if (text.Length > 0 && text[text.Length - 1] != '\n') text.Append(" · ");
            text.Append(part);
        }

        internal static string Rarity(ItemState state)
        {
            if (state.Rarity != null) return RarityPalette.Tag(state.Rarity) + DisplayWords.Name(state.Rarity.Name, state.Rarity.Id) + RarityPalette.Close;
            if (state.IsUnknownRarity) return RarityPalette.Grey + DisplayWords.Verbatim(state.RarityId!) + RarityPalette.Close;
            return InscribeText.Text("rarity_normal");
        }

        internal static string? Name(ItemDrop.ItemData item, bool identifyRarity = false)
        {
            if (item?.m_shared == null || !InscribeModules.Active(InscribeModule.Display)) return null;
            Summary(item);
            Entry entry = Summaries.GetOrCreateValue(item);
            if (!entry.HasName)
            {
                ItemState state = entry.State!;
                string? topic = entry.RarityDisplay ? DisplayCache.Topic(state, item) : null;
                entry.Name = InscribeExtraCommands.DecorateName(item, topic);
                entry.IdentifiedName = state.IsMagic ? Rarity(state) + " " + (entry.Name ?? item.m_shared.m_name) : entry.Name;
                entry.HasName = true;
            }
            return identifyRarity ? entry.IdentifiedName : entry.Name;
        }
    }
}

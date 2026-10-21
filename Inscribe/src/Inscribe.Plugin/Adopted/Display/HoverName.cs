// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Inscribe.Runtime;
using ExtraPotions.Inscribe.Integration;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Identifies an item above its native pickup prompt. The hover text is rebuilt by the game
    /// every frame while the player looks at the item, so the last input and output are remembered and a repeat costs
    /// one content compare and no allocation. Viewing client only.
    /// </summary>
    internal static class HoverName
    {
        private static string? _lastInput;
        private static ItemDrop.ItemData? _lastItem;
        private static string? _lastSummary;
        private static string? _lastTopic;
        private static string? _lastOutput;

        internal static string Identify(string text, ItemDrop.ItemData item)
        {
            if (!InscribeModules.Active(InscribeModule.Display)) return text;
            string summary = ItemIdentification.Summary(item);
            string? topic = ItemIdentification.Name(item, identifyRarity: true);
            if (ReferenceEquals(item, _lastItem) && ReferenceEquals(summary, _lastSummary)
                && ReferenceEquals(topic, _lastTopic) && string.Equals(text, _lastInput))
            {
                return _lastOutput!;
            }
            string name = Words.Localize(item.m_shared.m_name);
            string output = text;
            if (topic != null && InscribeModules.StepAside.RarityDisplay && name.Length > 0 && text.StartsWith(name, StringComparison.Ordinal))
                output = Words.Localize(topic) + text.Substring(name.Length);
            if (summary.Length > 0)
            {
                int end = output.IndexOf('\n');
                output = end < 0 ? output + "\n" + summary : output.Insert(end, "\n" + summary);
            }
            _lastInput = text;
            _lastItem = item;
            _lastSummary = summary;
            _lastTopic = topic;
            _lastOutput = output;
            return output;
        }
    }
}

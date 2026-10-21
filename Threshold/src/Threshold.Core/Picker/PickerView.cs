// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the picker window's model (T13b, design TH07): the list beside the map pane, one selection shared
// by both, the selected row's Travel and home hub actions (Threshold follow-up 2), the Return To Hub button (follow-ups
// 1 to 3) and Set or Clear hub on the portal walked into (B1 to B5).
using System;
using System.Collections.Generic;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Map;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Routing;

namespace ExtraPotions.Threshold.Picker
{
    public enum PickerLineKind { Header, Portal }

    /// <summary>What the window offers for the portal walked into: a hub is an untagged portal (HubRules).</summary>
    public enum SourceHubAction { None, SetHub, ClearHub }

    public sealed class PickerLine
    {
        internal PickerLine(PickerLineKind kind, PickerCategory category, PickerRow? row, int pin, bool home)
        {
            Kind = kind;
            Category = category;
            Row = row;
            Pin = pin;
            IsHome = home;
        }

        public PickerLineKind Kind { get; }

        /// <summary>The group the line is in (a favorite's row keeps its own category in <see cref="PickerRow.Category"/>).</summary>
        public PickerCategory Category { get; }

        public PickerRow? Row { get; }

        /// <summary>The map pin of the row (the same index in <see cref="MapPinPlan"/>), -1 for a header.</summary>
        public int Pin { get; }

        public bool IsHome { get; }
    }

    public sealed class PickerView
    {
        private readonly int[] lineOfPin;

        private PickerView(IReadOnlyList<PickerLine> lines, MapPinPlan plan, SourceHubAction sourceHub, int[] lineOfPin)
        {
            Lines = lines;
            Plan = plan;
            SourceHub = sourceHub;
            this.lineOfPin = lineOfPin;
        }

        public IReadOnlyList<PickerLine> Lines { get; }
        public MapPinPlan Plan { get; }
        public SourceHubAction SourceHub { get; }

        /// <summary>The hub of the Return To Hub button, or null for no button.</summary>
        public PortalEntry? ReturnHub => Plan.ReturnHub;

        public bool IsEmpty => Plan.DestinationCount == 0;

        public static PickerView Build(IReadOnlyList<PickerGroup> groups, PortalEntry? current, PortalEntry? returnHub, string? homeHubKey,
            bool accessModesEnabled = true, bool highlightStone = false)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            var plan = MapPinPlan.Build(groups, current, returnHub, homeHubKey, accessModesEnabled, highlightStone);
            var lines = new List<PickerLine>();
            var lineOfPin = new int[plan.Pins.Count];
            for (int i = 0; i < lineOfPin.Length; i++) lineOfPin[i] = -1;
            int pin = 0;
            foreach (var group in groups)
            {
                if (group.Rows.Count == 0) continue;
                lines.Add(new PickerLine(PickerLineKind.Header, group.Category, null, -1, false));
                foreach (var row in group.Rows)
                {
                    lineOfPin[pin] = lines.Count;
                    lines.Add(new PickerLine(PickerLineKind.Portal, group.Category, row, pin, plan.Pins[pin].IsHome));
                    pin++;
                }
            }
            var source = current == null ? SourceHubAction.None : current.IsHub ? SourceHubAction.ClearHub
                : current.IsTagged ? SourceHubAction.None : SourceHubAction.SetHub;
            return new PickerView(lines.AsReadOnly(), plan, source, lineOfPin);
        }

        /// <summary>The list line of a map pin, or -1 (this portal, the player, out of range).</summary>
        public int LineOfPin(int pin) => pin >= 0 && pin < lineOfPin.Length ? lineOfPin[pin] : -1;

        /// <summary>The portal Travel goes to for the selected line, or null (a header or nothing selected).</summary>
        public PortalEntry? Destination(int line) => line >= 0 && line < Lines.Count ? Plan.DestinationAt(Lines[line].Pin) : null;

        /// <summary>The home hub action for the selected line: set, clear on the current home, or null (not a hub).</summary>
        public string? HomeActionKey(int line)
        {
            if (line < 0 || line >= Lines.Count || Plan.HomeCandidateAt(Lines[line].Pin) == null) return null;
            return Lines[line].IsHome ? ThresholdTextKeys.PickerClearHome : ThresholdTextKeys.PickerSetHome;
        }

        public static string? TextKey(SourceHubAction action) =>
            action == SourceHubAction.SetHub ? ThresholdTextKeys.PickerSetHub : action == SourceHubAction.ClearHub ? ThresholdTextKeys.PickerClearHub : null;
    }
}

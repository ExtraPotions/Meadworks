// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Reuses Cairn's GamepadMap/all-held/one-down chord and C07 consumption pattern.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
namespace ExtraPotions.Magpie.Runtime
{
    internal static class MagpiePadChord
    {
        internal static PadButton[] Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 256) return Array.Empty<PadButton>();
            var names = text.Split('+');
            if (names.Length > 16) return Array.Empty<PadButton>();
            var buttons = new List<PadButton>();
            foreach (var name in names)
            {
                if (!GamepadMap.TryParse(name, out var button) || buttons.Contains(button)) return Array.Empty<PadButton>();
                buttons.Add(button);
            }
            return buttons.ToArray();
        }
        internal static bool Pressed(IReadOnlyList<PadButton> chord, bool active, bool allowed,
            Func<PadButton, bool> held, Func<PadButton, bool> down, Action<PadButton> consume)
        {
            if (!active || !allowed || chord.Count == 0) return false;
            bool anyDown = false;
            foreach (var button in chord)
            {
                if (!held(button)) return false;
                anyDown |= down(button);
            }
            if (!anyDown) return false;
            foreach (var button in chord) consume(button);
            return true;
        }
    }
}

// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; the same all-held/one-down/consume-all chord rule as Magpie's MagpiePadChord and Cairn's
// GamepadMap pattern (C07), kept generic so this Unity-free core stays free of the Crucible input types.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Wildblood.Integration
{
    public delegate bool PadButtonParser<T>(string name, out T button);

    public static class PadChord
    {
        public const int MaxButtons = 16;

        /// <summary>"A+B" style text to buttons; anything unknown, repeated or oversized binds nothing.</summary>
        public static IReadOnlyList<T> Parse<T>(string? text, PadButtonParser<T> parse)
        {
            if (string.IsNullOrWhiteSpace(text) || text!.Length > 256) return Array.Empty<T>();
            var names = text.Split('+');
            if (names.Length > MaxButtons) return Array.Empty<T>();
            var buttons = new List<T>();
            foreach (var name in names)
            {
                if (!parse(name.Trim(), out var button) || buttons.Contains(button)) return Array.Empty<T>();
                buttons.Add(button);
            }
            return buttons.AsReadOnly();
        }

        /// <summary>True once when every button is held and at least one went down this frame; the chord's buttons are
        /// then consumed so the game does not also act on them.</summary>
        public static bool Pressed<T>(IReadOnlyList<T> chord, bool active, bool allowed, Func<T, bool> held, Func<T, bool> down, Action<T> consume)
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

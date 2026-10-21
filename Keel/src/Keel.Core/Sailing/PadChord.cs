// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Gamepad Wind Call and Gamepad Lookout chords (design 4.4, C07): Crucible PadButton names joined
// by +, default empty so nothing collides with steering.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Keel.Sailing
{
    public static class PadChord
    {
        /// <summary>The button names of a chord text, trimmed, each once, in order; empty for none.</summary>
        public static IReadOnlyList<string> Names(string? text) =>
            (text ?? "").Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        private const int CacheLimit = 64;
        private static readonly Dictionary<string, IReadOnlyList<string>> parsed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        /// <summary>
        /// <see cref="Names"/> kept per chord text: the keys are read every frame, so the same text must not be split and
        /// filtered again each time. Main thread only; the list is cleared when it grows past a handful of distinct texts.
        /// </summary>
        public static IReadOnlyList<string> Cached(string? text)
        {
            var key = text ?? "";
            if (parsed.TryGetValue(key, out var names)) return names;
            if (parsed.Count >= CacheLimit) parsed.Clear();
            names = Names(key);
            parsed[key] = names;
            return names;
        }

        /// <summary>A chord fires on the frame its last button goes down: every button held, at least one pressed this frame.</summary>
        public static bool Pressed(IReadOnlyList<bool> held, IReadOnlyList<bool> down)
        {
            if (held == null || down == null || held.Count == 0 || held.Count != down.Count) return false;
            var anyDown = false;
            for (var i = 0; i < held.Count; i++)
            {
                if (!held[i]) return false;
                if (down[i]) anyDown = true;
            }
            return anyDown;
        }
    }
}

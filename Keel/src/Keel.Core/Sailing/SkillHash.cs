// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (same as Saga.Core, expDARE): the game's string hash, reimplemented exactly as Crucible CS1 does (Keel.Core references nothing
// beyond netstandard), so Keel can pin its skill numbers in portable tests and check CS1 agrees at run time.
using System;

namespace ExtraPotions.Keel.Sailing
{
    public static class SkillHash
    {
        /// <summary>
        /// The game's <c>GetStableHashCode</c>: two interleaved djb2-xor hashes over even and odd characters, stopping at
        /// the first NUL, combined as <c>a + b * 1566083941</c> with wrap-around.
        /// </summary>
        public static int Of(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            unchecked
            {
                int a = 5381, b = a;
                for (var i = 0; i < text.Length && text[i] != '\0'; i += 2)
                {
                    a = ((a << 5) + a) ^ text[i];
                    if (i == text.Length - 1 || text[i + 1] == '\0') break;
                    b = ((b << 5) + b) ^ text[i + 1];
                }
                return a + b * 1566083941;
            }
        }

        /// <summary>A custom skill's <c>Skills.SkillType</c> number: the hash masked positive with <c>&amp; 0x7FFFFFFF</c> (CS1, GrindstoneSkills).</summary>
        public static int CustomType(string identity) => Of(identity) & 0x7FFFFFFF;
    }
}

// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: choice values and name lists of design section 4.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Saga.Config
{
    /// <summary>Design 4.1 Skill Gain Notices (SNEAKer's Display Exp Gained, generalised).</summary>
    public enum SkillGainNotices { Off, SagaSkills, All }

    /// <summary>Design 4.2 Power Display Mode: how several attunements share the power HUD.</summary>
    public enum PowerDisplayMode { Stacked, Mixed, Compact, Icons, NewestOnly }

    /// <summary>Gamepad button that holds the power wheel open. Names match Crucible <c>PadButton</c>, plus Unbound.</summary>
    public enum PowerWheelPad { Unbound, A, B, X, Y, LeftBumper, RightBumper, LeftTrigger, RightTrigger, Back, Start, LeftStick, RightStick }

    /// <summary>Named bundle for one group of related numbers. Custom writes nothing.</summary>
    public enum SagaPreset { Vanilla, MeadWorksDefault, Relaxed, Custom }

    /// <summary>A comma list of names (item prefabs): trimmed, empty entries and repeats dropped, order kept.</summary>
    public static class NameList
    {
        public static IReadOnlyList<string> Parse(string? text) =>
            (text ?? "").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        public static string Normalize(string? text) => string.Join(", ", Parse(text));
    }
}

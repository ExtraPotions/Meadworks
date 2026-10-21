// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: choice values of design 4.2 to 4.4. Ranged, bomb and summon quality names are SecondaryAttacks'
// option names (used as names only); slope and swim modes follow the design and KeepWeaponsAfterSwim's documented values.
namespace ExtraPotions.Edge.Config
{
    /// <summary>Design 4.2 ranged group presets (SecondaryAttacks <c>RangedPresetSelection</c> names plus Off).</summary>
    public enum RangedPreset { Barrage, Volley, Piercing, Scatter, Spiral, Sentinel, Meteor, Burst, Off }

    /// <summary>Design 4.2 bomb preset: Auto picks Overcharged Bomb for bombs with an area, else Sticky Detonator.</summary>
    public enum BombPreset { Auto, StickyDetonator, OverchargedBomb, Off }

    /// <summary>Design 4.2 summon quality preset.</summary>
    public enum SummonQualityPreset { Off, CountByQuality, LevelByQuality }

    /// <summary>Design 4.3: Off, Fixed (Slope Combat Assistance behavior, user decision Q2 default) or Adaptive.</summary>
    public enum SlopeMode { Off, Fixed, Adaptive }

    /// <summary>Design 4.4 swim mode (KeepWeaponsAfterSwim's documented values).</summary>
    public enum SwimMode { AutoRestore, KeepInHand, Off }

    /// <summary>Canonical forms of free text settings.</summary>
    public static class TextRules
    {
        /// <summary>Carrier and marker texts are stored as written; null becomes empty.</summary>
        public static string Verbatim(string? text) => text ?? "";
    }
}

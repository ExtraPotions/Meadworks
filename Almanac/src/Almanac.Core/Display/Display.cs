// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Almanac.Display
{
    /// <summary>How a setting row is highlighted.</summary>
    public enum ChangeMark { Default, Modified, ChangedThisSession }

    public static class ChangeMarks
    {
        /// <summary>
        /// A value edited during this session is marked as such even when it returned to the default; otherwise a value
        /// that differs from the mod's reset target is Modified. Values are the serialized forms.
        /// </summary>
        public static ChangeMark Of(string current, string resetTarget, string? sessionBefore)
        {
            if (sessionBefore != null && !string.Equals(sessionBefore, current, StringComparison.Ordinal)) return ChangeMark.ChangedThisSession;
            return string.Equals(current, resetTarget, StringComparison.Ordinal) ? ChangeMark.Default : ChangeMark.Modified;
        }
    }

    public enum PauseAction { None, Pause, Unpause }

    /// <summary>
    /// Pauses the game while Almanac is open when the player asked for it. Only a pause Almanac made is released, and
    /// never while another holder (the pause menu) is still showing.
    /// </summary>
    public sealed class PauseLatch
    {
        public bool Owned { get; private set; }

        /// <param name="wanted">Almanac is open and the Pause Game preference is on.</param>
        /// <param name="paused">The game's pause request is currently set.</param>
        /// <param name="externalHold">Another pause holder is visible (the game menu).</param>
        public PauseAction Tick(bool wanted, bool paused, bool externalHold)
        {
            if (wanted)
            {
                if (paused) return PauseAction.None;
                Owned = true;
                return PauseAction.Pause;
            }
            return Release(paused, externalHold);
        }

        public PauseAction Release(bool paused, bool externalHold)
        {
            if (!Owned) return PauseAction.None;
            Owned = false;
            return paused && !externalHold ? PauseAction.Unpause : PauseAction.None;
        }
    }

    /// <summary>The settings window's size inside the canvas it is drawn on (UI units, which follow the game's interface scale).</summary>
    public static class WindowFit
    {
        /// <summary>The designed size, and the clear space kept to each canvas edge (the wood frame reaches 12 past the panel).</summary>
        public const float Width = 1100f, Height = 760f, Margin = 24f;

        /// <param name="halfWidthLimit">How far the centered window may reach either side of the center before it meets
        /// something it must not cover (the main menu's modded-version notice); infinity when nothing is beside it.</param>
        public static (float Width, float Height) Size(float canvasWidth, float canvasHeight, float halfWidthLimit = float.PositiveInfinity)
        {
            float width = Math.Min(Width, Math.Min(canvasWidth - 2 * Margin, 2 * (halfWidthLimit - Margin)));
            float height = Math.Min(Height, canvasHeight - 2 * Margin);
            return (Math.Max(320f, width), Math.Max(300f, height));
        }
    }

    /// <summary>Window Scale presets: the size of Almanac's own windows relative to their designed size.</summary>
    public static class WindowScale
    {
        public const string Default = "100%";
        public static readonly string[] Presets = { "80%", "90%", "100%", "115%", "130%" };

        /// <summary>The scale factor of a preset; anything else (a hand-edited value) is the designed size.</summary>
        public static float Factor(string? preset)
        {
            int at = Array.IndexOf(Presets, preset?.Trim());
            return at < 0 ? 1f : int.Parse(Presets[at].TrimEnd('%')) / 100f;
        }

        /// <summary>
        /// The factor for a fixed-size window: a larger preset grows it only as far as the canvas (with margins) allows,
        /// never below its designed size; smaller presets and 100% apply as chosen.
        /// </summary>
        public static float Bounded(float factor, float width, float height, float canvasWidth, float canvasHeight)
        {
            if (factor <= 1f) return factor;
            float room = Math.Min((canvasWidth - 2 * WindowFit.Margin) / width, (canvasHeight - 2 * WindowFit.Margin) / height);
            return Math.Max(1f, Math.Min(factor, room));
        }
    }

    /// <summary>
    /// A setting row's Text, Reset and Undo buttons: each as wide as its translated caption needs (never narrower than the
    /// English layout), so a long caption such as German "Zurücksetzen" keeps its size instead of auto-shrinking.
    /// </summary>
    public static class RowButtons
    {
        /// <summary>The English widths of the Text, Reset and Undo buttons.</summary>
        public const float Text = 56f, Reset = 68f, Undo = 68f;

        /// <summary>A button's width for a caption of <paramref name="caption"/> units (the button art keeps 9 units each side).</summary>
        public static float Width(float english, float caption) => Math.Max(english, (float)Math.Ceiling(caption + 18f));

        /// <summary>Everything right of the value control: a 6-unit gap, then the three buttons 4 apart.</summary>
        public static float Cluster(float text, float reset, float undo) => 6f + text + 4f + reset + 4f + undo;

        /// <summary>
        /// Where the value control starts and how wide it is in a row of <paramref name="rowWidth"/>: the name column keeps
        /// 46% of the row (at least 240) while the value keeps 160; wider buttons take the room from the name column first.
        /// </summary>
        public static (float X, float Value) Columns(float rowWidth, float cluster)
        {
            float x = Math.Max(160f, Math.Min(Math.Max(240f, rowWidth * .46f), rowWidth - cluster - 160f));
            return (x, Math.Max(100f, rowWidth - x - cluster));
        }
    }
}

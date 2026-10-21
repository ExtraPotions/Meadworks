// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: when a picker window failure turns the picker off.
namespace ExtraPotions.Threshold.Picker
{
    /// <summary>One exception tears the window down and the next open builds it again; only <see cref="Limit"/> failures
    /// in a row turn the picker off (portals stay the game's) until the next session.</summary>
    public sealed class PickerFailures
    {
        public const int Limit = 3;
        int inRow;

        public bool Disabled => inRow >= Limit;

        /// <summary>An open or redraw threw. True when this failure turned the picker off.</summary>
        public bool Failed()
        {
            if (Disabled) return false;
            inRow++;
            return Disabled;
        }

        public void Opened() => inRow = 0;

        public void Reset() => inRow = 0;
    }
}

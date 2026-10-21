// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the walk-in is decided inside the game's physics trigger, but the picker is built and opened on the next
// frame, because Unity forbids destroying objects at once during a trigger callback (window and kit setup does that).

namespace ExtraPotions.Threshold.Picker
{
    /// <summary>One open waiting for the next frame: the last request wins, and it is taken once.</summary>
    public sealed class DeferredOpen<T> where T : class
    {
        T? pending;

        public bool Pending => pending != null;

        public void Request(T value) => pending = value;

        public T? Take()
        {
            var value = pending;
            pending = null;
            return value;
        }

        public void Clear() => pending = null;
    }
}

// Kindle - ExtraPotions Meadworks crafting conveniences.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Kindle.Config
{
    /// <summary>
    /// Cheat values (design 4.5): a cheat the authority gate blocks evaluates to its safe value (Craft Speed 1.0, Cart
    /// Workbench off); an allowed cheat is the setting's value under its own rule. The gate decision comes from the plugin.
    /// </summary>
    public static class KindleCheats
    {
        public static T Effective<T>(KindleSetting<T> setting, bool locked, T value)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            if (setting.Scope != SettingScope.Cheat) throw new ArgumentException(setting.Id + " is not a cheat.", nameof(setting));
            return locked ? setting.Safe : setting.Normalize(value);
        }

        /// <summary>Craft Speed in effect: 1.0 when blocked, else clamped to 0.1 to 10 (non-finite reads 1.0).</summary>
        public static float CraftSpeed(bool locked, float value) => Effective(KindleSettings.Batch.CraftSpeed, locked, value);

        /// <summary>Instant Crafting in effect: off when blocked.</summary>
        public static bool InstantCrafting(bool locked, bool value) => Effective(KindleSettings.Batch.InstantCrafting, locked, value);

        /// <summary>How long a craft takes: none while Instant Crafting is in effect (the game finishes a craft whose timer reached its time), else <see cref="CraftDuration(float,float)"/>.</summary>
        public static float CraftDuration(float gameSeconds, float speed, bool instant) => instant ? 0f : CraftDuration(gameSeconds, speed);

        /// <summary>How long a craft takes at a speed: the game's time divided by the speed (normalized as Craft Speed).</summary>
        public static float CraftDuration(float gameSeconds, float speed)
        {
            if (!(gameSeconds > 0f) || float.IsInfinity(gameSeconds)) return gameSeconds > 0f ? gameSeconds : 0f;
            return gameSeconds / CraftSpeed(false, speed);
        }
    }
}

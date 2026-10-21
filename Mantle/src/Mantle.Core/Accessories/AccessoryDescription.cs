// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mantle.Accessories
{
    /// <summary>One line of an item's description: the key of its translated template and the number to put in it ("{0}").</summary>
    public sealed class StatLine
    {
        public StatLine(string key, string number)
        {
            Key = key;
            Number = number;
        }

        public string Key { get; }

        /// <summary>The size of the bonus without a sign; the template carries the sign and the unit.</summary>
        public string Number { get; }
    }

    /// <summary>The stat lines a ring's or belt's description shows, in a fixed order, for the stats the registry has now.</summary>
    public static class AccessoryDescription
    {
        public static IReadOnlyList<StatLine> Lines(AccessoryStats stats)
        {
            List<StatLine> lines = new List<StatLine>();
            Add(lines, "stat_max_health", stats.MaxHealth);
            Add(lines, "stat_max_stamina", stats.MaxStamina);
            Add(lines, "stat_max_eitr", stats.MaxEitr);
            Add(lines, "stat_carry", stats.Carry);
            Add(lines, "stat_stamina_regen", stats.StaminaRegenPercent);
            Add(lines, "stat_move_speed", stats.MoveSpeedPercent);
            Add(lines, stats.AttackStaminaPercent < 0f ? "stat_attack_stamina_less" : "stat_attack_stamina_more", stats.AttackStaminaPercent);
            return lines;
        }

        private static void Add(List<StatLine> lines, string key, float value)
        {
            if (value != 0f)
                lines.Add(new StatLine(key, Math.Abs(value).ToString("0.##", CultureInfo.InvariantCulture)));
        }
    }
}

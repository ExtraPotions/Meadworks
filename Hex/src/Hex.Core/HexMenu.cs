// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
namespace ExtraPotions.Hex;

public static class HexMenu
{
    // Seven pages: Fun and Speeds share Movement, Items and Creatures share Spawn (with a switch on the page). The
    // Achievements toggles, the menu and panic keys, the Mischief policy, ESP search/distance/filters and per-row hotkeys
    // are edited in Almanac only.
    public static IReadOnlyList<string> Tabs { get; } = new[] { "cheats", "movement", "skills", "spawn", "map", "debug", "presets" };
    public static IReadOnlyList<string> Speeds { get; } = new[] { "walk_speed", "run_speed", "swim_speed", "fly_speed", "mounted_speed" };
    public static IReadOnlyDictionary<string, string[]> Preferences { get; } = new Dictionary<string, string[]> {
        ["cheats"] = new[] { "god_mode", "ghost_mode", "fly", "noclip", "freecam", "unlimited_stamina", "unlimited_eitr", "infinite_jumps", "disable_durability_loss", "disable_food_degradation", "no_carry_limit", "custom_carry_weight", "keep_items", "keep_food", "keep_skills", "keep_status_effects", "quick_actions" },
        ["movement"] = new[] { "moon_gravity", "bouncy", "super_knockback", "party_hits", "jump_power", "player_size", "walk_speed", "run_speed", "swim_speed", "fly_speed", "mounted_speed" },
        ["skills"] = new[] { "skill_editor" }, ["spawn"] = new string[0], ["map"] = new[] { "map_tools" },
        ["debug"] = new[] { "debug_tools", "debug_mode", "esp", "local_time_minute", "local_time_freeze" },
        ["presets"] = new string[0] };

    /// <summary>Preferences with no row in the Hex window: Almanac shows them.</summary>
    public static IReadOnlyList<string> AlmanacOnly { get; } = new[] { "achievements_despite_cheats", "clear_inventory_marks", "clear_container_marks", "clear_drop_marks" };

    /// <summary>Local Time quick buttons inside the Local Time Minute row: each sets Local Time Minute.</summary>
    public static IReadOnlyList<(string Id, int Minute)> TimePresets { get; } = new[] { ("midnight", 0), ("sunrise", 360), ("noon", 720), ("sunset", 1080) };

    /// <summary>A preference row's value control starts here and its controls end at <see cref="RowEnd"/>.</summary>
    public const float ValueX = 380f, RowEnd = 708f;

    /// <summary>
    /// The Reset button grows to its caption (plus the button art's inset) instead of letting a long translation such as
    /// German "Zurücksetzen" auto-shrink to unreadable text; at least the English 68, at most 150, right-aligned at the row's end.
    /// </summary>
    public static (float X, float Width) ResetSpan(float captionWidth)
    {
        var width = System.Math.Min(150f, System.Math.Max(68f, captionWidth + 18f));
        return (RowEnd - width, width);
    }

    /// <summary>The value control's width: its own width unless the Reset button needs the room (10 units gap).</summary>
    public static float ValueWidth(float preferred, float resetX) => System.Math.Max(120f, System.Math.Min(preferred, resetX - 10f - ValueX));
}

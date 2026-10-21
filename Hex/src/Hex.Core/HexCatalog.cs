// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
namespace ExtraPotions.Hex;

public static class HexCatalog
{
    // Complete agreed inventory stays explicit; a partial unit never marks its whole group complete.
    public static IReadOnlyList<string> AgreedUnits { get; } = new[] {
        "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "B1", "B2", "B3", "C1", "C2", "C3", "C4",
        "D1", "E1", "E2", "F1", "F2", "F3", "G1", "G2", "G3", "G4", "G5" };
    public static IReadOnlyList<string> RuntimeKeys { get; } = new[] {
        "unlimited_stamina", "unlimited_eitr", "infinite_jumps", "walk_speed", "run_speed", "swim_speed", "jump_power", "moon_gravity", "disable_durability_loss",
        "no_carry_limit", "custom_carry_weight", "disable_food_degradation", "god_mode", "ghost_mode", "fly", "noclip", "freecam", "fly_speed", "mounted_speed", "keep_items", "keep_food", "keep_skills", "keep_status_effects", "achievements_despite_cheats", "clear_inventory_marks", "clear_container_marks", "clear_drop_marks", "local_time_minute", "local_time_freeze", "bouncy", "super_knockback", "party_hits", "player_size", "debug_mode", "esp" };

    public static float SpeedFactor(int percent) => Math.Min(300, Math.Max(10, percent)) / 100f;
    public static float SizeFactor(int percent) => Math.Min(300, Math.Max(25, percent)) / 100f;
    public static float JumpFactor(int percent) => Math.Min(500, Math.Max(25, percent)) / 100f;
    /// <summary>A4 precedence: No Carry Limit wins; a positive custom value is exact (1 to 99999); 0 keeps the native result.</summary>
    public static float CarryCapacity(float native, bool noLimit, int custom)
        => noLimit ? float.PositiveInfinity : custom > 0 ? Math.Min(99999, custom) : native;
}

/// <summary>A temporary native field contribution. A later writer keeps ownership of its changed value.</summary>
public readonly struct OwnedScale
{
    public readonly float Before, Applied;
    public OwnedScale(float before, float factor) { Before = before; Applied = before * factor; }
    public float Restore(float current) => current == Applied ? Before : current;
}

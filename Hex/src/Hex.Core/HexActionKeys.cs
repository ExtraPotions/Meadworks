// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
namespace ExtraPotions.Hex;

public static class HexActionKeys
{
    public static IReadOnlyList<string> Powers { get; } = new[] { "GP_Eikthyr", "GP_TheElder", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Queen", "GP_Ashlands", "GP_DeepNorth" };
    public static IReadOnlyList<string> Keys { get; } = new[] { "heal", "rested", "reveal", "poi", "estimatepoi", "seed", "reset", "reset_speeds", "spawn_protection",
        "GP_Eikthyr", "GP_TheElder", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Queen", "GP_Ashlands", "GP_DeepNorth" };
    public static string Label(string key) => key == "heal" ? "actions_heal" : key == "rested" ? "actions_rested"
        : key == "reveal" ? "map_reveal" : key == "poi" ? "poi_actual" : key == "estimatepoi" ? "poi_estimate"
        : key == "reset_speeds" ? "speeds_reset_all" : key == "seed" ? "seed_label" : key == "reset" ? "map_reset"
        : key == "spawn_protection" ? "spawn_protection_label" : "actions_power";
}

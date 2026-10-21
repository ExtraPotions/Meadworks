// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Hex;

// Same bounded native location selection contract as Thing C8, without a mandatory Thing assembly dependency.
// Keys are ZoneSystem location prefab names; DN_Bossroom is the Deep North boss room of Valheim 1.0.20, whose boss
// FrozenKing has Character m_name $enemy_frozenking (Kall Fimbulbringer).
public static class HexPoiRows
{
    public const string CapabilityId = "meadworks.thing.poi";
    public static readonly Version Version = new(1, 0);
    public const int Maximum = 512;
    static readonly Dictionary<string, string> names = new(StringComparer.Ordinal) {
        ["Eikthyrnir"] = "$enemy_eikthyr", ["GDKing"] = "$enemy_gdking", ["Bonemass"] = "$enemy_bonemass",
        ["Dragonqueen"] = "$enemy_dragon", ["GoblinKing"] = "$enemy_goblinking", ["Mistlands_DvergrBossEntrance1"] = "$enemy_seekerqueen",
        ["FaderLocation"] = "$enemy_fader", ["DN_Bossroom"] = "$enemy_frozenking",
        ["Vendor_BlackForest"] = "$npc_haldor", ["Hildir_camp"] = "$npc_hildir", ["BogWitch_Camp"] = "$npc_bogwitch" };
    public static bool Trader(string name) => name == "Vendor_BlackForest" || name == "Hildir_camp" || name == "BogWitch_Camp";
    public static string Token(string name) => names.TryGetValue(name, out var token) ? token : name;
    static bool Coordinate(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= 20000;
    public static IReadOnlyList<Tuple<string, float, float, float, bool>> Select(IEnumerable<Tuple<string, float, float, float, bool>> source)
    {
        var rows = new List<Tuple<string, float, float, float, bool>>();
        foreach (var row in source)
        {
            if (row == null || !names.ContainsKey(row.Item1 ?? "") || !Coordinate(row.Item2) || !Coordinate(row.Item3) || !Coordinate(row.Item4)) continue;
            rows.Add(row);
            if (rows.Count > Maximum) throw new InvalidOperationException("PoiLimit");
        }
        var confirmed = new HashSet<string>(rows.Where(row => Trader(row.Item1) && row.Item5).Select(row => row.Item1), StringComparer.Ordinal);
        return Array.AsReadOnly(rows.Where(row => !Trader(row.Item1) || row.Item5 || !confirmed.Contains(row.Item1)).Distinct().ToArray());
    }
}

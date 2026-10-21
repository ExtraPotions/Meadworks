// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Shepherd.Pens
{
    public static class LandPenDefinition
    {
        public const string Prefab = "shepherd_land_pen";
        public const string NativeGate = "wood_gate";
        public const string DefaultRecipe = "Wood:30, Resin:4";
        public const string Station = "piece_workbench";
        public const int DefaultCapacity = 12;
        public const int MaximumCapacity = 100;
        public const float Width = 6, Length = 6, Height = 3, RailHeight = 1.5f;
        public const string NameKey = "piece_land_pen";
        public const string DescriptionKey = "piece_land_pen_description";
        public const string CapacityKey = "land_pen_capacity";
        public static int Capacity(int value) => value < 0 ? 0 : value > MaximumCapacity ? MaximumCapacity : value;
    }
}

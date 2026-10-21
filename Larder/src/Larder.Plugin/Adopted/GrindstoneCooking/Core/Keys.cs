// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods 9da28eb58adbe065be1e4baef779a448acd38e0f: GrindstoneSkills/GrindstoneSkills/Core/Keys.cs
// Changed by expDARE, 2026: cooking keys only; owned writes and read-only legacy fallbacks.
namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    public static class Keys
    {
        public const string SlotLevel = "larder_level";
        public const string BaseLevel = "larder_base_level";
        public const string RpcAddItem = "larder_AddItem";
        public const string RpcAddBase = "larder_AddBase";
        public const string MadeDishes = "larder.made";
        public const string LegacyMadeDishes = "grindstone_made";
        public static string Legacy(string key) => key.StartsWith("larder_", System.StringComparison.Ordinal)
            ? "grindstone_" + key.Substring(7) : key;
        public static int ReadInt(ZDO zdo, string key, int fallback = 0) => zdo == null ? fallback
            : zdo.GetInt(key.GetStableHashCode(), zdo.GetInt(Legacy(key).GetStableHashCode(), fallback));
        public static float ReadFloat(ZDO zdo, string key, float fallback = 0f) => zdo == null ? fallback
            : zdo.GetFloat(key.GetStableHashCode(), zdo.GetFloat(Legacy(key).GetStableHashCode(), fallback));
        public static long ReadLong(ZDO zdo, string key, long fallback = 0L) => zdo == null ? fallback
            : zdo.GetLong(key.GetStableHashCode(), zdo.GetLong(Legacy(key).GetStableHashCode(), fallback));
    }
}

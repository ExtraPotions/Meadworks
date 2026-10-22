// Almanac - ExtraPotions Meadworks settings book.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Almanac
{
    /// <summary>Product identity shared by Almanac.Core and the plugin. Almanac.Core is Unity-free and BepInEx-free.</summary>
    public static class AlmanacCore
    {
        public const string Guid = "com.meadworks.almanac";
        public const string Name = "Almanac";
        public const string Version = "0.1.18";
        public const string Family = "Runestone";

        /// <summary>Data folder under BepInEx/config (profiles, backups, session journal).</summary>
        public const string DataFolder = "Meadworks/almanac";
    }
}

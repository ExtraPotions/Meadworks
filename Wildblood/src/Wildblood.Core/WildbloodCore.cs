// Wildblood - ExtraPotions Meadworks creature difficulty. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Wildblood
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class WildbloodCore
    {
        public const string PluginGuid = "com.meadworks.wildblood";
        public const string ProductName = "Wildblood";
        public const string ProductVersion = "0.5.8";
        public const string Family = "Fang";

        /// <summary>Localization namespace (design section 13): tokens <c>$meadworks_wildblood_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "wildblood";
        public const string TokenPrefix = "meadworks_wildblood_";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

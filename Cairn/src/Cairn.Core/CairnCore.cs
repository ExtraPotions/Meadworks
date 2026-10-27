// Cairn - ExtraPotions Meadworks exploration and visibility. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Cairn
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class CairnCore
    {
        public const string PluginGuid = "com.meadworks.cairn";
        public const string ProductName = "Cairn";
        public const string ProductVersion = "0.1.20";
        public const string Family = "Waystone";

        /// <summary>Localization namespace (design section 11): tokens <c>$meadworks_cairn_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "cairn";
        public const string TokenPrefix = "meadworks_cairn_";

        /// <summary>Character custom data prefix (design header).</summary>
        public const string DataPrefix = "Cairn.";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

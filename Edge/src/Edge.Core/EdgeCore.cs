// Edge - ExtraPotions Meadworks combat. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Edge
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class EdgeCore
    {
        public const string PluginGuid = "com.meadworks.edge";
        public const string ProductName = "Edge";
        public const string ProductVersion = "0.2.13";
        public const string Family = "Fang";

        /// <summary>Localization namespace (design section 12): tokens <c>$meadworks_edge_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "edge";
        public const string TokenPrefix = "meadworks_edge_";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

// Solstice - ExtraPotions Meadworks seasons. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Solstice
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class SolsticeCore
    {
        public const string PluginGuid = "com.meadworks.solstice";

        /// <summary>The plugin GUID before 0.1.3. Its settings file and import marker are carried over once; other MeadWorks products accept either GUID for one release.</summary>
        public const string LegacyPluginGuid = "com.extrapotions.solstice";
        public const string ProductName = "Solstice";
        public const string ProductVersion = "0.1.9";
        public const string Family = "Runestone";

        /// <summary>Localisation namespace: tokens <c>$meadworks_solstice_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "solstice";
        public const string TokenPrefix = "meadworks_solstice_";

        /// <summary>Game build the product was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

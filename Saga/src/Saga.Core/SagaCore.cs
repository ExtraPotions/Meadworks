// Saga - ExtraPotions Meadworks progression. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Saga
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class SagaCore
    {
        public const string PluginGuid = "com.meadworks.saga";
        public const string ProductName = "Saga";
        public const string ProductVersion = "0.1.12";
        public const string Family = "Fang";

        /// <summary>Localization namespace (design section 12): tokens <c>$meadworks_saga_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "saga";
        public const string TokenPrefix = "meadworks_saga_";

        /// <summary>Character data prefix (design header): every key Saga stores on a player starts with it.</summary>
        public const string DataPrefix = "saga.";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

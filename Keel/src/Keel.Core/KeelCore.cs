// Keel - ExtraPotions Meadworks ships and sailing. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Keel
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class KeelCore
    {
        public const string PluginGuid = "com.meadworks.keel";
        public const string ProductName = "Keel";
        public const string ProductVersion = "0.1.12";
        public const string Family = "Waystone";

        /// <summary>Localization namespace (design section 11): tokens <c>$meadworks_keel_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "keel";
        public const string TokenPrefix = "meadworks_keel_";

        /// <summary>Prefix of every ZDO key Keel writes (design section 3): <c>keel.shipwright</c>, <c>keel.wind_dir</c>.</summary>
        public const string ZdoPrefix = "keel.";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}
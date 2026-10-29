// Threshold - ExtraPotions Meadworks portals. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Threshold
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class ThresholdCore
    {
        public const string PluginGuid = "com.meadworks.threshold";
        public const string ProductName = "Threshold";
        public const string ProductVersion = "0.3.13";
        public const string Family = "Waystone";

        /// <summary>Localization namespace (design section 11): tokens <c>$meadworks_threshold_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "threshold";
        public const string TokenPrefix = "meadworks_threshold_";

        /// <summary>Prefix of Threshold's portal ZDO keys (design header).</summary>
        public const string ZdoPrefix = "threshold.";

        /// <summary>Prefix of Threshold's character data keys (<c>Player.m_customData</c>).</summary>
        public const string CharacterDataPrefix = "Threshold.";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

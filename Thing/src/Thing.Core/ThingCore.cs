// Thing - ExtraPotions Meadworks server administration. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Thing
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class ThingCore
    {
        public const string PluginGuid = "com.meadworks.thing";
        public const string ProductName = "Thing";
        public const string ProductVersion = "0.1.11";
        public const string Family = "Runestone";

        /// <summary>Localization namespace (design section 11): tokens <c>$meadworks_thing_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "thing";
        public const string TokenPrefix = "meadworks_thing_";

        /// <summary>
        /// Server data folder relative to <c>BepInEx/config</c> (design header): ban and mute ledgers, audit log, announcements,
        /// remote-edit backups. Always forward slashes; the plugin joins it with the platform separator.
        /// </summary>
        public const string DataFolder = "Meadworks/thing";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

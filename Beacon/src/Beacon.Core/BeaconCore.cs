// Beacon - ExtraPotions Meadworks server favorites, auto-join and protected password storage.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Beacon
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class BeaconCore
    {
        public const string PluginGuid = "com.meadworks.beacon";
        public const string ProductName = "Beacon";
        public const string ProductVersion = "0.1.16";
        public const string Family = "Runestone";

        /// <summary>Localization namespace (design section 11): tokens <c>$meadworks_beacon_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "beacon";
        public const string TokenPrefix = "meadworks_beacon_";

        /// <summary>
        /// Data folder relative to <c>BepInEx/config</c> (design header): the server list and, on Windows, the protected
        /// password blobs. Always forward slashes; the plugin joins it with the platform separator.
        /// </summary>
        public const string DataFolder = "Meadworks/beacon";

        /// <summary>Server list: names, addresses, aliases, default flag, order and secret references. Never a password.</summary>
        public const string ServerListFile = "servers.json";

        /// <summary>Windows Data Protection blobs, one line per secret reference (design 4.2).</summary>
        public const string WindowsSecretFile = "secrets.dat";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

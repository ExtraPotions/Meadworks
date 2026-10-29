// Tempest - ExtraPotions Meadworks world weather events. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Tempest
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class TempestCore
    {
        public const string PluginGuid = "com.meadworks.tempest";
        public const string ProductName = "Tempest";
        public const string ProductVersion = "0.1.19";
        public const string Family = "Runestone";

        /// <summary>Localization namespace (design section 11): tokens <c>$meadworks_tempest_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "tempest";
        public const string TokenPrefix = "meadworks_tempest_";

        /// <summary>
        /// The world key that holds the shared event record (design 1.2, W5, W11; user decision 2026-10-08 item 6). The
        /// <c>mw.</c> prefix makes it a Crucible-guarded world key (CS4 world settings): the game saves it with the world
        /// and sends it to every peer, and Crucible lets only the server write it.
        /// </summary>
        public const string WorldKey = "mw.tempest.event";

        /// <summary>Guarded world key holding the dry mark of the current event (<see cref="Events.DryMark"/>).</summary>
        public const string DryWorldKey = "mw.tempest.lightningstorm";

        /// <summary>Guarded world key holding an event of a kind older clients do not know (<see cref="Events.ExtendedRecord"/>).</summary>
        public const string ExtendedWorldKey = "mw.tempest.extended";

        /// <summary>The plain global key session 1 used; read only, so an old record migrates once to <see cref="WorldKey"/>.</summary>
        public const string LegacyWorldKey = "tempest.event";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

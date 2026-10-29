// Delve - ExtraPotions Meadworks dungeons and world regeneration. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Delve
{
    /// <summary>Product identity shared by the plugin, the tests and the diagnostics.</summary>
    public static class DelveCore
    {
        public const string PluginGuid = "com.meadworks.delve";
        public const string ProductName = "Delve";
        public const string ProductVersion = "0.1.18";
        public const string Family = "Runestone";

        /// <summary>Localization namespace (design section 11): tokens <c>$meadworks_delve_&lt;key&gt;</c>.</summary>
        public const string TokenNamespace = "delve";
        public const string TokenPrefix = "meadworks_delve_";

        /// <summary>Prefix of every ZDO key Delve writes (design section 3): <c>delve.chest.stamp</c>, <c>delve.site.visit</c>.</summary>
        public const string ZdoPrefix = "delve.";

        /// <summary>
        /// Crucible C12 persisted instance identity key of a Delve site (a dungeon generator or a boss place), the form
        /// <c>InstanceIdentity.KeyFor("delve")</c> gives. Site state survives world loads under this token.
        /// </summary>
        public const string InstanceKey = "delve.instance";

        /// <summary>Game build the design was written against (Valheim 1.0.17).</summary>
        public const string SupportedGameVersion = "1.0.17";
    }
}

// Barter - ExtraPotions Meadworks trader overhaul.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Barter
{
    /// <summary>Barter.Core identity: Unity-free logic shared by the plugin and the portable tests.</summary>
    public static class BarterCore
    {
        public const string ProductId = "com.meadworks.barter";

        /// <summary>The plugin GUID before 0.1.2. Its settings file is carried over once; other MeadWorks products accept either GUID for one release.</summary>
        public const string LegacyProductId = "com.extrapotions.barter";

        /// <summary>Prefix of every trade list file Barter reads from the config folder (Barter.Haldor.Buy.yml).</summary>
        public const string FilePrefix = "Barter.";

        /// <summary>Prefab name of the vanilla currency, used when no list or trader names another one.</summary>
        public const string VanillaCurrency = "Coins";

        /// <summary>The trader name whose lists apply to every trader.</summary>
        public const string CommonTrader = "common";

        /// <summary>
        /// A purchase's quality travels inside the vanilla trade item's stack (stack + quality * this), because the
        /// game's trade item has no quality field. Copied from TradersExtended 2.0.5 (Unlicense) so list files and
        /// stacks stay interchangeable with it.
        /// </summary>
        public const int QualityStackMultiplier = 1000000;
    }
}

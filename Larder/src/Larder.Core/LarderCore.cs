// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Larder
{
    /// <summary>Larder.Core identity: Unity-free rules shared by the plugin and the portable tests.</summary>
    public static class LarderCore
    {
        public const string ProductId = "com.meadworks.larder";
        public const string Name = "Larder";
        public const string Version = "0.2.19";
        public const string Family = "Hearth";

        /// <summary>Valheim build the design was written against (design header); reconfirmed natively at L01b.</summary>
        public const string GameBuild = "1.0.17";

        /// <summary>Prefix of every key Larder stores in the character's custom data.</summary>
        public const string CharacterDataPrefix = "larder.";

        /// <summary>Prefix of every key Larder stores in a ZDO (station slots, barrels).</summary>
        public const string ZdoPrefix = "larder_";

        /// <summary>Prefix of Larder's localization tokens.</summary>
        public const string TokenPrefix = "$meadworks_larder_";
    }
}

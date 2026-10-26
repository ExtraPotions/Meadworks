// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Shepherd
{
    /// <summary>
    /// Product identity shared by Shepherd.Core and the plugin. Shepherd.Core is Unity-free (netstandard2.1, no game,
    /// Unity, BepInEx or Crucible reference): it holds the decision logic the plugin's Harmony patches call.
    /// </summary>
    public static class ShepherdCore
    {
        public const string ProductId = "com.meadworks.shepherd";
        public const string Name = "Shepherd";
        public const string Version = "0.1.21";
        public const string Family = "Hearth";

        /// <summary>The game build the design and research were read from (design header).</summary>
        public const string SupportedGameVersion = "1.0.17";

        /// <summary>Prefix of every Shepherd ZDO key, RPC name and egg custom-data key.</summary>
        public const string KeyPrefix = "shepherd.";
    }
}

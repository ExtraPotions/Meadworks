// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: carries untouched saved defaults forward when a default changes.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Wildblood.Config
{
    /// <summary>
    /// A saved value that is exactly an earlier default was never chosen by anyone, so it moves to the current default.
    /// Any other saved value is the owner's choice and stays as it is.
    /// </summary>
    public static class DefaultUpgrades
    {
        public const string DeepNorthKey = "defeated_frozenking_p3";

        /// <summary>The Boss Keys default before the Deep North boss (2026-10-19).</summary>
        public const string BossKeysBeforeDeepNorth =
            "defeated_eikthyr, defeated_gdking, defeated_bonemass, defeated_dragon, defeated_goblinking, defeated_queen, defeated_fader";

        /// <summary>The Protected Prefabs default before Kall's first phase joined it (2026-10-19).</summary>
        public const string ProtectedPrefabsBeforeDeepNorth = "FrozenKing_p2";

        static readonly Dictionary<WildbloodSetting<string>, string> earlier = new Dictionary<WildbloodSetting<string>, string>
        {
            [WildbloodSettings.WorldTier.BossKeys] = BossKeysBeforeDeepNorth,
            [WildbloodSettings.General.ProtectedPrefabs] = ProtectedPrefabsBeforeDeepNorth,
        };

        /// <summary>The current default when <paramref name="saved"/> is exactly the earlier default; otherwise null.</summary>
        public static string? Upgrade(WildbloodSetting setting, string? saved) =>
            setting is WildbloodSetting<string> text && saved != null && earlier.TryGetValue(text, out var old)
                && string.Equals(saved, old, StringComparison.Ordinal) ? text.Default : null;

        /// <summary>A customized Boss Keys list that leaves the Deep North boss out (worth one log line).</summary>
        public static bool MissesDeepNorth(WildbloodSetting setting, string? saved) =>
            ReferenceEquals(setting, WildbloodSettings.WorldTier.BossKeys) && saved != null
                && !NameList.Parse(saved).Any(k => string.Equals(k, DeepNorthKey, StringComparison.OrdinalIgnoreCase));
    }
}

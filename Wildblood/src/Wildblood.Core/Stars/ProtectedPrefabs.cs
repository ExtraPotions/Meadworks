// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; the protected-prefab idea and its FrozenKing_p2 default follow CreatureManager 1.2.5 (names only).
using System;
using ExtraPotions.Wildblood.Config;

namespace ExtraPotions.Wildblood.Stars
{
    /// <summary>
    /// The Protected Prefabs list (design A11): creatures that are never starred, traited or scaled. CreatureManager hard-codes
    /// FrozenKing_p2, whose health drives its seven-Aspect encounter; Wildblood reads the list from its setting instead, so the list
    /// is authoritative (clearing it unprotects FrozenKing_p2). Its default also holds Kall Fimbulbringer's first phase, FrozenKing:
    /// stars or a Twin there would be lost or doubled at the phase change. The final phase, FrozenKing_p3, stays open so its
    /// defeat counts for boss progress and rewards.
    /// </summary>
    public static class ProtectedPrefabs
    {
        const string CloneSuffix = "(Clone)";

        /// <summary>The prefab name without a trailing "(Clone)" and surrounding padding.</summary>
        public static string PrefabName(string? name)
        {
            var text = (name ?? "").Trim();
            if (text.EndsWith(CloneSuffix, StringComparison.Ordinal)) text = text.Substring(0, text.Length - CloneSuffix.Length).TrimEnd();
            return text;
        }

        /// <summary>Whether <paramref name="prefabName"/> is one of the comma-separated names in <paramref name="list"/> (whole name, case-blind).</summary>
        public static bool Contains(string? list, string? prefabName)
        {
            var name = PrefabName(prefabName);
            if (name.Length == 0) return false;
            foreach (var entry in NameList.Parse(list))
                if (string.Equals(entry, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>The console spawn rule for a protected prefab: only a plain level-one creature (CreatureManager's cm:spawn rule).</summary>
        public static bool AllowsSpawn(bool isProtected, int stars, int traitWords) => !isProtected || (stars <= 0 && traitWords <= 0);
    }
}

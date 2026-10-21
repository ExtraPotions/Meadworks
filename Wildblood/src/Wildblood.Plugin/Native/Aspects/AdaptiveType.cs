// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Traits;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The damage types an Adaptive boss tracks and resists: the eight a player chooses between by picking a weapon.
    /// Chop and pickaxe are left out - they are tool damage that no boss takes - and so is the game's untyped true
    /// damage, which by definition nothing resists. The value is what the boss's ZDO stores, so append, never renumber.
    /// </summary>
    public enum AdaptiveType
    {
        None = 0,
        Blunt = 1,
        Slash = 2,
        Pierce = 3,
        Fire = 4,
        Frost = 5,
        Lightning = 6,
        Poison = 7,
        Spirit = 8,
    }

    /// <summary>
    /// Reading and cutting one <see cref="AdaptiveType"/> in a hit's damage, and the single seam between the resisted
    /// type and the boss's ZDO. Reads work on every machine; only the boss's owner writes, and only on a change.
    /// </summary>
    internal static class AdaptiveTypes
    {
        private static readonly int AdaptedHash = TraitKeys.Adapted.GetStableHashCode();

        /// <summary>The highest type value; the tracked types run from 1 to this.</summary>
        public const int Last = (int)AdaptiveType.Spirit;

        public static float Amount(HitData.DamageTypes damage, AdaptiveType type) => type switch
        {
            AdaptiveType.Blunt => damage.m_blunt,
            AdaptiveType.Slash => damage.m_slash,
            AdaptiveType.Pierce => damage.m_pierce,
            AdaptiveType.Fire => damage.m_fire,
            AdaptiveType.Frost => damage.m_frost,
            AdaptiveType.Lightning => damage.m_lightning,
            AdaptiveType.Poison => damage.m_poison,
            AdaptiveType.Spirit => damage.m_spirit,
            _ => 0f,
        };

        /// <summary>Multiplies the one type's share of the hit by the factor, leaving the rest alone.</summary>
        public static void Scale(HitData hit, AdaptiveType type, float factor)
        {
            switch (type)
            {
                case AdaptiveType.Blunt: hit.m_damage.m_blunt *= factor; break;
                case AdaptiveType.Slash: hit.m_damage.m_slash *= factor; break;
                case AdaptiveType.Pierce: hit.m_damage.m_pierce *= factor; break;
                case AdaptiveType.Fire: hit.m_damage.m_fire *= factor; break;
                case AdaptiveType.Frost: hit.m_damage.m_frost *= factor; break;
                case AdaptiveType.Lightning: hit.m_damage.m_lightning *= factor; break;
                case AdaptiveType.Poison: hit.m_damage.m_poison *= factor; break;
                case AdaptiveType.Spirit: hit.m_damage.m_spirit *= factor; break;
            }
        }

        /// <summary>The type the boss resists now, as its owner wrote it; none for no ZDO or a bad value.</summary>
        public static AdaptiveType Read(ZDO? zdo)
        {
            int value = zdo != null ? zdo.GetInt(AdaptedHash) : 0;
            return value >= 0 && value <= Last ? (AdaptiveType)value : AdaptiveType.None;
        }

        public static void Write(ZDO zdo, AdaptiveType type) => zdo.Set(TraitKeys.Adapted, (int)type);

        /// <summary>The type as `wildblood inspect` names it: "fire", or "nothing" when no type dominates.</summary>
        public static string Describe(AdaptiveType type) =>
            type == AdaptiveType.None ? "nothing" : type.ToString().ToLowerInvariant();
    }
}

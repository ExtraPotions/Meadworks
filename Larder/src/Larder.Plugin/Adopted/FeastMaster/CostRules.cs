// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/CostRules.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>Saved field values for a drain site whose fields are scaled for the duration of one call.</summary>
    public struct ScaledFields
    {
        public bool Set;
        public float First;
        public float Second;
        public float Third;
    }

    /// <summary>Up to three independently scaled sites of one call; each is left alone while its multiplier is 1.</summary>
    public struct ScaledGroup
    {
        public ScaledFields First;
        public ScaledFields Second;
        public ScaledFields Third;
    }

    /// <summary>The rules shared by the stamina cost patches: out of combat, free sneaking and the skill discount.</summary>
    public static class CostRules
    {
        /// <summary>Not targeted, not sensed and not attacking (Player.IsTargeted / IsSensed / Character.InAttack).</summary>
        public static bool OutOfCombat(Player player)
        {
            return !player.IsTargeted() && !player.IsSensed() && !player.InAttack();
        }

        /// <summary>The out of combat multiplier when the player is out of combat, else 1.</summary>
        public static float OutOfCombatFactor(Player player, float outOfCombatCost)
        {
            if (outOfCombatCost == 1f)
                return 1f;
            return OutOfCombat(player) ? outOfCombatCost : 1f;
        }

        /// <summary>1 minus the skill discount at the player's level of the given skill.</summary>
        public static float SkillDiscountFactor(Player player, Skills.SkillType skill)
        {
            float discount = NativeFeastMaster.Value(Settings.SkillDiscount);
            if (discount == 0f)
                return 1f;
            return Mathf.Max(0f, 1f - discount / 100f * player.GetSkillFactor(skill));
        }

        /// <summary>
        /// Free sneaking: no enemy within stealth range (BaseAI.InStealthRange false), and none has noticed the
        /// player, since InStealthRange is also false while an alerted enemy is close.
        /// </summary>
        public static bool SneakingIsFree(Player player)
        {
            if (!NativeFeastMaster.Value(Settings.FreeSneakingWithoutEnemies))
                return false;
            return !BaseAI.InStealthRange(player) && !player.IsSensed() && !player.IsTargeted();
        }

        public static ScaledFields Scale(ref float field, float multiplier)
        {
            if (multiplier == 1f)
                return default;
            ScaledFields saved = new ScaledFields { Set = true, First = field };
            field *= multiplier;
            return saved;
        }

        public static ScaledFields Scale(ref float first, ref float second, float multiplier)
        {
            if (multiplier == 1f)
                return default;
            ScaledFields saved = new ScaledFields { Set = true, First = first, Second = second };
            first *= multiplier;
            second *= multiplier;
            return saved;
        }

        /// <summary>Replaces a field with a value for the duration of one call (restored with <see cref="Restore(ref float, ScaledFields)"/>).</summary>
        public static ScaledFields Swap(ref float field, float value)
        {
            if (field == value)
                return default;
            ScaledFields saved = new ScaledFields { Set = true, First = field };
            field = value;
            return saved;
        }

        public static void Restore(ref float field, ScaledFields saved)
        {
            if (saved.Set)
                field = saved.First;
        }

        public static void Restore(ref float first, ref float second, ScaledFields saved)
        {
            if (!saved.Set)
                return;
            first = saved.First;
            second = saved.Second;
        }
    }
}

// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/BaseValues.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Base values: a changed Base Health or Base Stamina is written into the player's field right before the game
    /// sums base and food values (so the HUD base bar and the totals follow, hot reloaded on the next food update),
    /// and the stamina from skills is added to the stamina total afterwards. A base value at its default is not
    /// written, so the game's or another mod's value stands; see <see cref="BaseOriginals"/>.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
    public static class BaseValuesPatch
    {
        public static bool Prepare()
        {
            return Customized.Any(Settings.BaseHealth, Settings.BaseStamina, Settings.RunSkillStamina, Settings.JumpSkillStamina,
                Settings.SneakSkillStamina, Settings.SwimSkillStamina, Settings.FishingSkillStamina);
        }

        /// <summary>No base setting is changed any more: the players get their own values back.</summary>
        public static void Removed() => BaseOriginals.RestoreAll();

        [HarmonyPrefix]
        public static void Prefix(Player __instance) => BaseOriginals.Write(__instance);

        [HarmonyPostfix]
        public static void Postfix(Player __instance, ref float stamina)
        {
            stamina += SkillBonus(__instance);
        }

        /// <summary>The total stamina the player's skills add to the base, per the section 5 settings.</summary>
        public static float SkillBonus(Player player)
        {
            return Bonus(player, Skills.SkillType.Run, NativeFeastMaster.Value(Settings.RunSkillStamina))
                + Bonus(player, Skills.SkillType.Jump, NativeFeastMaster.Value(Settings.JumpSkillStamina))
                + Bonus(player, Skills.SkillType.Sneak, NativeFeastMaster.Value(Settings.SneakSkillStamina))
                + Bonus(player, Skills.SkillType.Swim, NativeFeastMaster.Value(Settings.SwimSkillStamina))
                + Bonus(player, Skills.SkillType.Fishing, NativeFeastMaster.Value(Settings.FishingSkillStamina));
        }

        private static float Bonus(Player player, Skills.SkillType skill, float atSkill100)
        {
            return atSkill100 == 0f ? 0f : atSkill100 * player.GetSkillFactor(skill);
        }
    }

    /// <summary>
    /// The player's own base health and stamina (the game's, or what another mod set), kept when a configured value
    /// first replaces them and put back when that setting returns to its default or the patch is removed.
    /// </summary>
    public static class BaseOriginals
    {
        private sealed class Kept
        {
            public float? Health;
            public float? Stamina;
        }

        private static readonly ConditionalWeakTable<Player, Kept> kept = new ConditionalWeakTable<Player, Kept>();

        public static void Write(Player player)
        {
            Kept own = kept.GetOrCreateValue(player);
            own.Health = Replace(ref player.m_baseHP, Settings.BaseHealth, own.Health);
            own.Stamina = Replace(ref player.m_baseStamina, Settings.BaseStamina, own.Stamina);
        }

        /// <summary>A changed setting replaces the field (its own value kept the first time); a default one puts it back.</summary>
        private static float? Replace(ref float field, ConfigEntry<float> setting, float? own)
        {
            if (Customized.Any(setting))
            {
                float original = own ?? field;
                field = NativeFeastMaster.Value(setting);
                return original;
            }
            if (own.HasValue)
                field = own.Value;
            return null;
        }

        public static void RestoreAll()
        {
            foreach (Player player in Player.s_players)
            {
                if (player == null || !kept.TryGetValue(player, out Kept own))
                    continue;
                if (own.Health.HasValue)
                    player.m_baseHP = own.Health.Value;
                if (own.Stamina.HasValue)
                    player.m_baseStamina = own.Stamina.Value;
                kept.Remove(player);
            }
        }
    }
}

using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The numbers behind a held item's cost and block lines (<see cref="HeldStatLines"/>), worked out as the game does,
    /// and their breakdowns. Attack stamina (<c>Attack.GetAttackStamina</c>): the attack's cost, the gear's attack stamina
    /// modifier (the build modifier for a hammer or hoe), the status effects' <c>ModifyAttackStaminaUsage</c>, a third
    /// off at skill 100, and the stamina some weapons give back for missing health. Stamina held while drawing
    /// (<c>Player.UpdateAttackBowDraw</c>): the draw's cost after the skill, the gear and the effects. Eitr and health:
    /// the cost, a third off at skill 100. Block armour: the item's (quality included), half again at Blocking 100. Parry:
    /// the item's bonus, each effect's <c>ModifyTimedBlockBonus</c>, and the block armour a parry makes of it.
    /// </summary>
    public static class HeldTips
    {
        private const float SkillCut = 0.33f;
        private const float Probe = 100f;

        public static float AttackStamina(Player player, ItemDrop.ItemData weapon)
        {
            Attack attack = weapon.m_shared.m_attack;
            float use = attack.m_attackStamina;
            if (use <= 0f)
                return 0f;
            use *= 1f + GearModifier(player, attack);
            player.GetSEMan().ModifyAttackStaminaUsage(use, ref use);
            use = AfterSkill(player, weapon, use);
            if (attack.m_staminaReturnPerMissingHP > 0f)
                use -= (player.GetMaxHealth() - player.GetHealth()) * attack.m_staminaReturnPerMissingHP;
            return use;
        }

        public static float DrawStamina(Player player, ItemDrop.ItemData weapon)
        {
            float drain = weapon.GetDrawStaminaDrain();
            if (drain <= 0f)
                return 0f;
            drain += drain * player.GetEquipmentAttackStaminaModifier();
            player.GetSEMan().ModifyAttackStaminaUsage(drain, ref drain);
            return drain;
        }

        /// <summary>The attack's health cost before the skill: a flat part and a share of the health the player has now.</summary>
        public static float HealthCost(Player player, Attack attack) =>
            attack.m_attackHealth + player.GetHealth() * attack.m_attackHealthPercentage / 100f;

        /// <summary>A cost after the weapon's skill: a third off at skill 100 (<c>Attack.GetAttackHealth</c>, <c>GetAttackEitr</c>).</summary>
        public static float AfterSkill(Player player, ItemDrop.ItemData weapon, float cost) =>
            cost - cost * SkillCut * player.GetSkillFactor(weapon.m_shared.m_skillType);

        /// <summary>The build modifier for a home item (hammer, hoe), else the attack stamina modifier.</summary>
        private static float GearModifier(Player player, Attack attack) =>
            attack.m_isHomeItem ? player.GetEquipmentHomeItemModifier() : player.GetEquipmentAttackStaminaModifier();

        public static string Stamina(Player player, ItemDrop.ItemData weapon, float cost)
        {
            Attack attack = weapon.m_shared.m_attack;
            TipText tip = new TipText().Part(StatTipWords.Base, cost.ToString("0.#"));
            float gear = GearModifier(player, attack);
            if (Mathf.Abs(gear) >= 0.005f)
                tip.Part(Player.s_equipmentModifierTooltips[attack.m_isHomeItem ? 1 : 4], TipText.Percent(gear));
            foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
            {
                float use = Probe;
                effect.ModifyAttackStaminaUsage(Probe, ref use);
                if (Mathf.Abs(use - Probe) >= 0.5f)
                    tip.Part(TipText.EffectName(effect), TipText.Percent(use / Probe - 1f));
            }
            return Skill(player, weapon.m_shared.m_skillType, tip, -SkillCut).ToString();
        }

        /// <summary>A cost the skill alone lowers (eitr, health): the base, then the skill's cut.</summary>
        public static string Cost(Player player, ItemDrop.ItemData weapon, float cost) =>
            Skill(player, weapon.m_shared.m_skillType, new TipText().Part(StatTipWords.Base, cost.ToString("0.#")), -SkillCut).ToString();

        public static string Block(Player player, ItemDrop.ItemData item)
        {
            TipText tip = new TipText().Part(item.m_shared.m_name, TipText.Whole(item.GetBaseBlockPower()));
            return Skill(player, Skills.SkillType.Blocking, tip, 0.5f).ToString();
        }

        /// <summary>The parry's multiplier on block armour: the item's, then every effect's.</summary>
        public static float ParryBonus(Player player, ItemDrop.ItemData item)
        {
            float bonus = item.m_shared.m_timedBlockBonus;
            player.GetSEMan().ModifyTimedBlockBonus(ref bonus);
            return bonus;
        }

        public static string Parry(Player player, ItemDrop.ItemData item)
        {
            TipText tip = new TipText().Part(item.m_shared.m_name, item.m_shared.m_timedBlockBonus.ToString("0.##") + "x");
            foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
            {
                float bonus = 1f;
                effect.ModifyTimedBlockBonus(ref bonus);
                if (Mathf.Abs(bonus - 1f) >= 0.005f)
                    tip.Part(TipText.EffectName(effect), TipText.Percent(bonus - 1f));
            }
            float armour = item.GetBlockPower(player.GetSkillFactor(Skills.SkillType.Blocking)) * ParryBonus(player, item);
            return tip.Part(StatTipWords.ParryArmour, TipText.Whole(armour)).ToString();
        }

        /// <summary>The skill's part at the player's level: <paramref name="atHundred"/> of the value at skill 100.</summary>
        private static TipText Skill(Player player, Skills.SkillType skill, TipText tip, float atHundred)
        {
            float part = atHundred * player.GetSkillFactor(skill);
            return Mathf.Abs(part) >= 0.005f ? tip.Part(SkillLabel(player, skill), TipText.Percent(part)) : tip;
        }

        /// <summary>The skill's own name and the player's level in it ("Swords 23").</summary>
        public static string SkillLabel(Player player, Skills.SkillType skill)
        {
            if (skill == Skills.SkillType.None)
                return StatTipWords.Skill;
            return "$skill_" + skill.ToString().ToLowerInvariant() + " " + Mathf.FloorToInt(player.GetSkillLevel(skill));
        }
    }
}

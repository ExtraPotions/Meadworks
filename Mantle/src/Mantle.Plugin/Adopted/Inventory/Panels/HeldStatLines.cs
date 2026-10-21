using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The stat sheet's lines for what the player holds (the user asked for equipped weapons and shields, 2026-09-28): a
    /// section per held item (<see cref="HeldItems"/>: the right hand's, then the left's, sheathed ones included) headed
    /// by the item's name. A weapon: each damage type as the range one hit does now (<see cref="HeldDamage"/>), what its
    /// primary attack costs (stamina, eitr, health, stamina held while drawing; <see cref="HeldTips"/>), knockback (the
    /// ammo's added) and the backstab multiplier. The item that blocks (the left hand's, else the weapon): block armour
    /// with the Blocking skill, block force, the parry bonus with the effects', parry adrenaline, and the damage
    /// modifiers it applies to a blocked hit. Labels are the game's own item tooltip words; a section with no line (a
    /// hammer) shows nothing.
    /// </summary>
    public static class HeldStatLines
    {
        public static void Fill(Player player, StatSheet sheet)
        {
            ItemDrop.ItemData right = HeldItems.Right(player);
            ItemDrop.ItemData left = HeldItems.Left(player);
            ItemDrop.ItemData blocker = HeldItems.Blocker(player);
            if (right != null)
                Item(player, right, right == blocker, sheet);
            if (left != null && left != right)
                Item(player, left, left == blocker, sheet);
        }

        private static void Item(Player player, ItemDrop.ItemData item, bool blocks, StatSheet sheet)
        {
            sheet.Section(item.m_shared.m_name);
            if (item.IsWeapon())
            {
                HeldDamage.Lines(player, item, sheet);
                Costs(player, item, sheet);
                Impact(player, item, sheet);
            }
            if (blocks)
                Block(player, item, sheet);
        }

        private static void Costs(Player player, ItemDrop.ItemData weapon, StatSheet sheet)
        {
            Attack attack = weapon.m_shared.m_attack;
            float stamina = HeldTips.AttackStamina(player, weapon);
            if (stamina > 0f)
                sheet.Add("$item_staminause", stamina.ToString("0.#"), HeldTips.Stamina(player, weapon, attack.m_attackStamina));
            float eitr = attack.GetAttackEitr(player, weapon);
            if (eitr > 0f)
                sheet.Add("$item_eitruse", eitr.ToString("0.#"), HeldTips.Cost(player, weapon, attack.m_attackEitr));
            float health = HeldTips.HealthCost(player, attack);
            if (health > 0f)
                sheet.Add("$item_healthuse", HeldTips.AfterSkill(player, weapon, health).ToString("0.#"), HeldTips.Cost(player, weapon, health));
            float draw = HeldTips.DrawStamina(player, weapon);
            if (draw > 0f)
                sheet.Add("$item_staminahold", draw.ToString("0.#") + "/s", HeldTips.Stamina(player, weapon, attack.m_drawStaminaDrain));
        }

        private static void Impact(Player player, ItemDrop.ItemData weapon, StatSheet sheet)
        {
            ItemDrop.ItemData ammo = HeldItems.Ammo(player, weapon);
            float force = weapon.m_shared.m_attackForce + (ammo != null ? ammo.m_shared.m_attackForce : 0f);
            if (force > 0f)
                sheet.Add("$item_knockback", force.ToString("0"));
            if (weapon.m_shared.m_backstabBonus > 1f)
                sheet.Add("$item_backstab", weapon.m_shared.m_backstabBonus.ToString("0.##") + "x");
        }

        private static void Block(Player player, ItemDrop.ItemData item, StatSheet sheet)
        {
            if (item.GetBaseBlockPower() > 1f)
            {
                float power = item.GetBlockPower(player.GetSkillFactor(Skills.SkillType.Blocking));
                sheet.Add("$item_blockarmor", power.ToString("0"), HeldTips.Block(player, item));
            }
            float force = item.GetDeflectionForce();
            if (force > 1f)
                sheet.Add("$item_blockforce", force.ToString("0"));
            if (item.m_shared.m_timedBlockBonus > 1f)
                sheet.Add("$item_parrybonus", HeldTips.ParryBonus(player, item).ToString("0.##") + "x", HeldTips.Parry(player, item));
            if (item.m_shared.m_perfectBlockAdrenaline > 0f)
                sheet.Add("$item_parryadrenaline", item.m_shared.m_perfectBlockAdrenaline.ToString("0.#"));
            BlockModifiers(item, sheet);
        }

        /// <summary>What the item makes of a blocked hit's damage types ("Fire: Resistant"), as the game applies it in a block.</summary>
        private static void BlockModifiers(ItemDrop.ItemData item, StatSheet sheet)
        {
            if (item.m_shared.m_damageModifiers == null)
                return;
            foreach (HitData.DamageModPair pair in item.m_shared.m_damageModifiers)
            {
                if (pair.m_modifier == HitData.DamageModifier.Normal || pair.m_modifier == HitData.DamageModifier.Ignore)
                    continue;
                sheet.Add("$inventory_" + pair.m_type.ToString().ToLowerInvariant(), GearStatLines.ModifierText(pair.m_modifier));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The breakdowns behind the stat sheet's first numbers, shown when a line is hovered (the user asked for them,
    /// 2026-09-28), summed as the game sums them. Health, stamina and eitr: the game's base (<c>m_baseHP</c>,
    /// <c>m_baseStamina</c>; eitr has none), then each food eaten with what it gives now (it shrinks as the food runs
    /// out). Armour: each worn helmet, chest, legs and cape (<c>GetBodyArmor</c> counts only those, then the effects).
    /// Weight: the carry weight's base (<c>m_maxCarryWeight</c>), each status effect adding to it (Megingjord's belt), the
    /// world's carry modifier, then the heaviest stacks carried. Anything another mod adds is the "Other effects" line.
    /// </summary>
    public static class StatTips
    {
        private const int HeaviestShown = 5;

        public static string Health(Player player) => Pool(player, player.GetMaxHealth(), player.m_baseHP, food => food.m_health);

        public static string Stamina(Player player) => Pool(player, player.GetMaxStamina(), player.m_baseStamina, food => food.m_stamina);

        public static string Eitr(Player player) => Pool(player, player.GetMaxEitr(), 0f, food => food.m_eitr);

        private static string Pool(Player player, float total, float start, Func<Player.Food, float> part)
        {
            TipText tip = new TipText();
            if (start > 0f)
                tip.Part(StatTipWords.Base, TipText.Whole(start));
            float sum = start;
            foreach (Player.Food food in player.m_foods)
            {
                float value = part(food);
                if (value < 0.5f || food.m_item == null)
                    continue;
                tip.Part(food.m_item.m_shared.m_name, "+" + TipText.Whole(value));
                sum += value;
            }
            return tip.Rest(total - sum, TipText.Signed).ToString();
        }

        public static string Armor(Player player)
        {
            TipText tip = new TipText();
            float sum = 0f;
            foreach (ItemDrop.ItemData item in TipText.Worn(player))
            {
                float armor = IsArmorPiece(item) ? item.GetArmor() : 0f;
                if (armor < 0.5f)
                    continue;
                tip.Part(item.m_shared.m_name, TipText.Whole(armor));
                sum += armor;
            }
            return tip.Rest(player.GetBodyArmor() - sum, TipText.Signed).ToString();
        }

        /// <summary>Helmet, chest, legs or cape: what the game's body armour and damage modifiers count.</summary>
        public static bool IsArmorPiece(ItemDrop.ItemData item)
        {
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            return type == ItemDrop.ItemData.ItemType.Helmet || type == ItemDrop.ItemData.ItemType.Chest
                || type == ItemDrop.ItemData.ItemType.Legs || type == ItemDrop.ItemData.ItemType.Shoulder;
        }

        public static string Weight(Player player)
        {
            TipText tip = new TipText().Heading(StatTipWords.CarryWeight);
            float sum = player.m_maxCarryWeight;
            tip.Part(StatTipWords.Base, TipText.Whole(sum));
            foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
            {
                if (!(effect is SE_Stats stats) || Mathf.Abs(stats.m_addMaxCarryWeight) < 0.5f)
                    continue;
                tip.Part(TipText.EffectName(effect), TipText.Signed(stats.m_addMaxCarryWeight));
                sum += stats.m_addMaxCarryWeight;
            }
            float rate = Game.m_carryWeightRate;
            if (!Mathf.Approximately(rate, 1f))
                tip.Part(StatTipWords.World, "x" + rate.ToString("0.##"));
            tip.Rest(player.GetMaxCarryWeight() - sum * rate, value => float.IsPositiveInfinity(value) ? "∞" : TipText.Signed(value));
            Heaviest(player, tip);
            return tip.ToString();
        }

        /// <summary>The heaviest stacks carried, heaviest first.</summary>
        private static void Heaviest(Player player, TipText tip)
        {
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(player.GetInventory().GetAllItems());
            items.RemoveAll(item => item.GetWeight() < 0.5f);
            if (items.Count == 0)
                return;
            items.Sort((a, b) => b.GetWeight().CompareTo(a.GetWeight()));
            tip.Heading(StatTipWords.Heaviest);
            for (int i = 0; i < items.Count && i < HeaviestShown; i++)
            {
                ItemDrop.ItemData item = items[i];
                string name = item.m_stack > 1 ? Mantle.Core.Language.Localize(item.m_shared.m_name) + " x" + item.m_stack : item.m_shared.m_name;
                tip.Part(name, TipText.Whole(item.GetWeight()));
            }
        }
    }
}

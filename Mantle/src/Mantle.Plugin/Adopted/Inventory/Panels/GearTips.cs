using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The breakdowns of the stat sheet's gear lines (<see cref="StatTips"/> has the first numbers'). Movement: each worn
    /// piece's movement modifier, the worn backpack's level, then each status effect's speed modifier. Durability: every equipped item that wears
    /// out, most worn first. A resistance: each armour piece (the game's <c>ApplyArmorDamageMods</c>: helmet, chest, legs,
    /// cape; a shield's count only in a block, <see cref="HeldStatLines"/>) and each status effect that changes that
    /// damage type, with what it makes of it. The other equipment modifiers: each worn piece's value of the game's own source field
    /// (<c>Player.s_equipmentModifierSourceFields</c>, what <c>m_equipmentModifierValues</c> sums), the rest of the
    /// game's total with effects (<c>GetEquipmentModifierPlusSE</c>) as "Other effects".
    /// </summary>
    public static class GearTips
    {
        public static string Movement(Player player)
        {
            TipText tip = new TipText();
            foreach (ItemDrop.ItemData item in TipText.Worn(player))
            {
                float modifier = item.m_shared.m_movementModifier;
                if (Mathf.Abs(modifier) >= 0.005f)
                    tip.Part(item.m_shared.m_name, TipText.Percent(modifier));
            }
            ItemDrop.ItemData pack = Backpacks.Backpack.WornItem(player);
            if (pack != null)
                tip.Part(pack.m_shared.m_name, TipText.Percent(Backpacks.BackpackLevels.Speed(pack.m_quality)));
            foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
            {
                if (effect is SE_Stats stats && Mathf.Abs(stats.m_speedModifier) >= 0.005f)
                    tip.Part(TipText.EffectName(effect), TipText.Percent(stats.m_speedModifier));
            }
            return tip.ToString();
        }

        public static string Durability(Player player)
        {
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in TipText.Worn(player))
            {
                if (item.m_shared.m_useDurability)
                    items.Add(item);
            }
            items.Sort((a, b) => a.GetDurabilityPercentage().CompareTo(b.GetDurabilityPercentage()));
            TipText tip = new TipText();
            foreach (ItemDrop.ItemData item in items)
                tip.Part(item.m_shared.m_name, (item.GetDurabilityPercentage() * 100f).ToString("0") + "%");
            return tip.ToString();
        }

        public static string Resistance(Player player, HitData.DamageType type)
        {
            TipText tip = new TipText();
            foreach (ItemDrop.ItemData item in TipText.Worn(player))
            {
                if (StatTips.IsArmorPiece(item))
                    AddMods(tip, item.m_shared.m_name, item.m_shared.m_damageModifiers, type);
            }
            foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
            {
                if (effect is SE_Stats stats)
                    AddMods(tip, TipText.EffectName(effect), stats.m_mods, type);
            }
            return tip.ToString();
        }

        private static void AddMods(TipText tip, string source, List<HitData.DamageModPair> mods, HitData.DamageType type)
        {
            if (mods == null)
                return;
            foreach (HitData.DamageModPair pair in mods)
            {
                if (pair.m_type == type && pair.m_modifier != HitData.DamageModifier.Normal)
                    tip.Part(source, GearStatLines.ModifierText(pair.m_modifier));
            }
        }

        /// <summary>The equipment modifier at <paramref name="index"/> in the game's list, shown as its sheet line is.</summary>
        public static string Modifier(Player player, int index)
        {
            FieldInfo[] fields = Player.s_equipmentModifierSourceFields;
            if (fields == null || index >= fields.Length)
                return "";
            TipText tip = new TipText();
            float sum = 0f;
            foreach (ItemDrop.ItemData item in TipText.Worn(player))
            {
                float value = fields[index].GetValue(item.m_shared) is float read ? read : 0f;
                if (Mathf.Abs(value) < 0.001f)
                    continue;
                tip.Part(item.m_shared.m_name, Format(index, value));
                sum += value;
            }
            float rest = player.GetEquipmentModifierPlusSE(index) - sum;
            return Mathf.Abs(rest) >= 0.001f ? tip.Part(StatTipWords.Other, Format(index, rest)).ToString() : tip.ToString();
        }

        /// <summary>As the sheet shows the modifier: a percentage, or from index 10 on a plain number.</summary>
        private static string Format(int index, float value) => index >= 10 ? value.ToString("+0;-0") : TipText.Percent(value);
    }
}

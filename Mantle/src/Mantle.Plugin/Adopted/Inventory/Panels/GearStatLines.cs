using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// What the stat sheet lists, worked out as the game does, labelled with the game's own words ($item_, $inventory_,
    /// $se_ tokens) so it follows the player's language. First, always: max health, stamina and eitr (eitr once there
    /// is any), armour (<c>GetBodyArmor</c>, effects included), carried weight over carry weight (red when over),
    /// movement speed (the gear's modifier, then the status effects' on top, as the game walks) and the lowest
    /// durability of anything equipped (red under a quarter). Then each damage type the player resists or is weak to
    /// (<c>GetDamageModifiers</c>: armour and status effects), green or red. Then every other equipment modifier that is
    /// not zero (stamina use, heat resistance, adrenaline), with status effects where the game's own tooltip totals them.
    /// Mantle's extra utilities count in every sum, since its patches add them to the game's.
    /// </summary>
    public static class GearStatLines
    {
        private const string Good = "#9CD66B";
        private const string Bad = "#FF7B6B";

        private static readonly HitData.DamageType[] DamageTypes =
        {
            HitData.DamageType.Blunt, HitData.DamageType.Slash, HitData.DamageType.Pierce, HitData.DamageType.Fire,
            HitData.DamageType.Frost, HitData.DamageType.Lightning, HitData.DamageType.Poison, HitData.DamageType.Spirit,
        };

        /// <summary>Health, stamina, eitr, armour, weight, movement and durability.</summary>
        public static void Core(Player player, StatSheet sheet)
        {
            Pools(player, sheet);
            Body(player, sheet);
        }

        private static void Pools(Player player, StatSheet sheet)
        {
            sheet.Add("$item_food_health", player.GetMaxHealth().ToString("0"), StatTips.Health(player));
            sheet.Add("$item_food_stamina", player.GetMaxStamina().ToString("0"), StatTips.Stamina(player));
            float eitr = player.GetMaxEitr();
            if (eitr > 0f)
                sheet.Add("$item_food_eitr", eitr.ToString("0"), StatTips.Eitr(player));
        }

        private static void Body(Player player, StatSheet sheet)
        {
            sheet.Add("$item_armor", player.GetBodyArmor().ToString("0"), StatTips.Armor(player));
            float weight = player.GetInventory().GetTotalWeight();
            float carry = player.GetMaxCarryWeight();
            string carried = weight.ToString("0") + "/" + (float.IsPositiveInfinity(carry) ? "∞" : carry.ToString("0"));
            sheet.Add("$item_weight", weight > carry ? Colour(carried, Bad) : carried, StatTips.Weight(player));
            sheet.Add("$item_movement_modifier", Percent(Movement(player)), GearTips.Movement(player));
            float durability = LowestDurability(player);
            string worn = durability < 0f ? "-" : (durability * 100f).ToString("0") + "%";
            sheet.Add("$item_durability", durability >= 0f && durability < 0.25f ? Colour(worn, Bad) : worn, GearTips.Durability(player));
        }

        /// <summary>As the game walks: the gear's modifier on the base speed, then every effect's on that, never below 0.</summary>
        private static float Movement(Player player)
        {
            float effects = 0f;
            foreach (StatusEffect effect in player.GetSEMan().GetStatusEffects())
                if (effect is SE_Stats stats)
                    effects += stats.m_speedModifier;
            return (1f + player.GetEquipmentMovementModifier()) * Mathf.Max(0f, 1f + effects) - 1f;
        }

        /// <summary>The lowest durability, 0 to 1, of the equipped items that wear out; -1 with none.</summary>
        private static float LowestDurability(Player player)
        {
            float lowest = -1f;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (!item.m_equipped || !item.m_shared.m_useDurability)
                    continue;
                float left = item.GetDurabilityPercentage();
                lowest = lowest < 0f ? left : Mathf.Min(lowest, left);
            }
            return lowest;
        }

        public static void Resistances(Player player, StatSheet sheet)
        {
            HitData.DamageModifiers mods = player.GetDamageModifiers();
            foreach (HitData.DamageType type in DamageTypes)
            {
                HitData.DamageModifier mod = mods.GetModifier(type);
                if (mod == HitData.DamageModifier.Normal || mod == HitData.DamageModifier.Ignore)
                    continue;
                sheet.Add("$inventory_" + type.ToString().ToLowerInvariant(), ModifierText(mod), GearTips.Resistance(player, type));
            }
        }

        /// <summary>The game's word for a damage modifier ("Resistant"), green when it helps, red when it hurts.</summary>
        public static string ModifierText(HitData.DamageModifier mod) =>
            Colour("$inventory_" + mod.ToString().ToLowerInvariant(), IsGood(mod) ? Good : Bad);

        private static bool IsGood(HitData.DamageModifier mod) =>
            mod == HitData.DamageModifier.Resistant || mod == HitData.DamageModifier.VeryResistant
            || mod == HitData.DamageModifier.SlightlyResistant || mod == HitData.DamageModifier.Immune;

        /// <summary>
        /// Every equipment modifier after movement (the game's list, index 0 is movement) that is not zero, named and
        /// totalled as the game's item tooltip does: percentages, the ones from index 10 on as plain numbers.
        /// </summary>
        public static void Modifiers(Player player, StatSheet sheet)
        {
            float[] values = player.m_equipmentModifierValues;
            string[] names = Player.s_equipmentModifierTooltips;
            if (values == null)
                return;
            for (int i = 1; i < values.Length && i < names.Length; i++)
            {
                float value = player.GetEquipmentModifierPlusSE(i);
                if (Mathf.Abs(value) >= 0.001f)
                    sheet.Add(names[i], i >= 10 ? value.ToString("+0;-0") : Percent(value), GearTips.Modifier(player, i));
            }
        }

        private static string Percent(float fraction) => (fraction * 100f).ToString("+0;-0;0") + "%";

        private static string Colour(string text, string hex) => "<color=" + hex + ">" + text + "</color>";
    }
}

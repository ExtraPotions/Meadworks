using HarmonyLib;
using Mantle.Core;
using Mantle.Backpacks;

namespace Mantle.Worn
{
    /// <summary>
    /// Every game rule that reads the one utility field also counts the extra utilities (<see cref="ExtraUtilities"/>):
    /// the equipment status effects, set counts, eitr regen, the equipment modifiers (movement, heat resistance, jump,
    /// attack stamina and the rest of the game's list), equipment weight and durability drain. Local player only; the
    /// lists are empty for everyone else.
    /// </summary>
    public static class UtilityEffectPatches
    {
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UpdateEquipmentStatusEffects))]
        public static class StatusEffects
        {
            [HarmonyPostfix]
            public static void Postfix(Humanoid __instance) => ExtraEffects.Sync(__instance);
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetSetCount))]
        public static class SetCount
        {
            [HarmonyPostfix]
            public static void Postfix(Humanoid __instance, string setName, ref int __result)
            {
                if (!InventoryState.IsLocal(__instance))
                    return;
                foreach (ItemDrop.ItemData item in ExtraUtilities.Worn)
                {
                    if (item.m_shared.m_setName == setName)
                        __result++;
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetEquipmentEitrRegenModifier))]
        public static class EitrRegen
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance, ref float __result)
            {
                if (!InventoryState.IsLocal(__instance))
                    return;
                foreach (ItemDrop.ItemData item in ExtraUtilities.Worn)
                    __result += item.m_shared.m_eitrRegenModifier;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.UpdateModifiers))]
        public static class Modifiers
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance)
            {
                if (!InventoryState.IsLocal(__instance) || Player.s_equipmentModifierSourceFields == null)
                    return;
                float[] values = __instance.m_equipmentModifierValues;
                if (values.Length > 0)
                    values[0] += BackpackLevels.WornSpeed(__instance);
                for (int i = 0; i < values.Length; i++)
                {
                    foreach (ItemDrop.ItemData item in ExtraUtilities.Worn)
                        values[i] += (float)Player.s_equipmentModifierSourceFields[i].GetValue(item.m_shared);
                }
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetEquipmentWeight))]
        public static class Weight
        {
            [HarmonyPostfix]
            public static void Postfix(Humanoid __instance, ref float __result)
            {
                if (!InventoryState.IsLocal(__instance))
                    return;
                foreach (ItemDrop.ItemData item in ExtraUtilities.Worn)
                    __result += item.m_shared.m_weight;
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UpdateEquipment))]
        public static class Durability
        {
            [HarmonyPostfix]
            public static void Postfix(Humanoid __instance, float dt)
            {
                if (!InventoryState.IsLocal(__instance))
                    return;
                // Backwards: a utility that breaks is taken off, which removes it from the list.
                for (int i = ExtraUtilities.Worn.Count - 1; i >= 0; i--)
                {
                    ItemDrop.ItemData item = ExtraUtilities.Worn[i];
                    if (item.m_shared.m_useDurability)
                        __instance.DrainEquipedItemDurability(item, dt);
                }
            }
        }
    }
}

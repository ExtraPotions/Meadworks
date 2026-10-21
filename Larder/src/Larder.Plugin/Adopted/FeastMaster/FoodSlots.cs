// Adopted from ValheimMods FeastMaster/FeastMaster/FeastMasterCore/FoodSlots.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Larder namespaces, Meadworks configuration/module lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Larder.Runtime.Adopted.FeastMaster
{
    /// <summary>
    /// Food Slots. The game allows three foods with the literal 3 in exactly two places: Player.CanEat ("full" at
    /// 3) and Player.EatFood (adds a new food below 3, otherwise replaces the most depleted one). Everything else
    /// (the totals, the regen, the save, which stores the count) works with any number. The two prefixes step in
    /// only where the configured count and 3 lead to different outcomes; every other case runs the game's method.
    /// Foods beyond a lowered count are kept until they run out. Foods live on the player's own client.
    /// </summary>
    public static class FoodSlots
    {
        public const int GameSlots = 3;
        public const int MaxSlots = 5;

        public static bool HasSame(Player player, ItemDrop.ItemData item)
        {
            foreach (Player.Food food in player.m_foods)
            {
                if (food.m_item.m_shared.m_name == item.m_shared.m_name)
                    return true;
            }
            return false;
        }

        public static bool AnyCanEatAgain(Player player)
        {
            foreach (Player.Food food in player.m_foods)
            {
                if (food.CanEatAgain())
                    return true;
            }
            return false;
        }

        /// <summary>The game's eat: the message, the new or replaced food, the statistics and the forced food update.</summary>
        public static bool Eat(Player player, ItemDrop.ItemData item, bool add)
        {
            Player.Food food = add ? new Player.Food() : player.GetMostDepletedFood();
            if (food == null)
                return false;
            player.Message(MessageHud.MessageType.Center, EatenText(item.m_shared));
            food.m_name = item.m_dropPrefab != null ? item.m_dropPrefab.name : food.m_name;
            food.m_item = item;
            food.m_time = item.m_shared.m_foodBurnTime;
            food.m_health = item.m_shared.m_food;
            food.m_stamina = item.m_shared.m_foodStamina;
            food.m_eitr = item.m_shared.m_foodEitr;
            if (add)
                player.m_foods.Add(food);
            Game.instance.IncrementPlayerStat(PlayerStatType.FoodEaten);
            Game.instance.GetPlayerProfile().IncrementStatFoodEaten(item.m_shared.m_name, 1f, item.m_cheated);
            player.UpdateFood(0f, forceUpdate: true);
            return true;
        }

        private static string EatenText(ItemDrop.ItemData.SharedData shared)
        {
            string text = "";
            if (shared.m_food > 0f)
                text += " +" + shared.m_food + " $item_food_health ";
            if (shared.m_foodStamina > 0f)
                text += " +" + shared.m_foodStamina + " $item_food_stamina ";
            if (shared.m_foodEitr > 0f)
                text += " +" + shared.m_foodEitr + " $item_food_eitr ";
            return text;
        }
    }

    /// <summary>
    /// CanEat. A food already eaten keeps the game's rule (eat again or "no more"). Otherwise, with more slots, a
    /// player holding 3 to Food Slots - 1 foods is not full; with fewer, a player holding Food Slots foods or more is
    /// full unless one of them can be eaten again (it is then replaced), as the game does at 3.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.CanEat))]
    public static class FoodSlotsCanEatPatch
    {
        public static bool Prepare() => Customized.Any(Settings.FoodSlots) || NativeFeastMaster.EatAnything;

        [HarmonyPrefix]
        public static bool Prefix(Player __instance, ItemDrop.ItemData item, bool showMessages, ref bool __result)
        {
            // Keep the game's same-food rule (the separate EatAgain patch reads1) and non-food checks.
            if (NativeFeastMaster.EatAnything && item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable
                && (item.m_shared.m_food > 0f || item.m_shared.m_foodStamina > 0f || item.m_shared.m_foodEitr > 0f)
                && !FoodSlots.HasSame(__instance,item))
            { __result = true; return false; }
            int slots = NativeFeastMaster.Value(Settings.FoodSlots);
            int count = __instance.m_foods.Count;
            if (slots == FoodSlots.GameSlots || FoodSlots.HasSame(__instance, item))
                return true;
            if (slots > FoodSlots.GameSlots)
            {
                if (count < FoodSlots.GameSlots || count >= slots)
                    return true;
                __result = true;
                return false;
            }
            if (count < slots)
                return true;
            __result = FoodSlots.AnyCanEatAgain(__instance);
            if (!__result && showMessages)
                __instance.Message(MessageHud.MessageType.Center, "$msg_isfull");
            return false;
        }
    }

    /// <summary>
    /// EatFood. A new food is added while fewer than Food Slots are held, otherwise the most depleted one is
    /// replaced; the game's method runs whenever it would do the same (it adds below 3). Low priority, so the
    /// consume-time value safety net (<see cref="PlayerEatFoodPatch"/>) has written the item first.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.EatFood))]
    public static class FoodSlotsEatPatch
    {
        public static bool Prepare() => Customized.Any(Settings.FoodSlots);

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        public static bool Prefix(Player __instance, ItemDrop.ItemData item, ref bool __result)
        {
            int slots = NativeFeastMaster.Value(Settings.FoodSlots);
            if (slots == FoodSlots.GameSlots || FoodSlots.HasSame(__instance, item) || !__instance.CanEat(item, showMessages: false))
                return true;
            int count = __instance.m_foods.Count;
            bool add = count < slots;
            if (add == (count < FoodSlots.GameSlots))
                return true;
            __result = FoodSlots.Eat(__instance, item, add);
            return false;
        }
    }
}

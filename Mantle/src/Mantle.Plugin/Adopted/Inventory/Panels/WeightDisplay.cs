using HarmonyLib;
using Mantle.Core;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The same stacked carry-weight fraction in the inventory and on the HUD, including overload flashing. The HUD asks
    /// every frame, so the text is built only when the weight, the most or the flash changes, and the same string is
    /// returned otherwise.
    /// </summary>
    public static class WeightDisplay
    {
        private static string last;
        private static int lastWeight, lastMax;
        private static bool lastRed;
        private static bool lastUnlimited;

        public static string Format(Player player)
        {
            int weight = Mathf.CeilToInt(player.GetInventory().GetTotalWeight());
            float capacity = player.GetMaxCarryWeight();
            bool unlimited = float.IsPositiveInfinity(capacity);
            int max = unlimited ? 0 : Mathf.CeilToInt(capacity);
            bool red = !unlimited && weight > max && Mathf.Sin(Time.time * 10f) > 0f;
            if (last == null || weight != lastWeight || max != lastMax || red != lastRed || unlimited != lastUnlimited)
            {
                last = Build(weight, max, red, unlimited);
                lastWeight = weight;
                lastMax = max;
                lastRed = red;
                lastUnlimited = unlimited;
            }
            return last;
        }

        private static string Build(int weight, int max, bool red, bool unlimited)
        {
            string current = red ? $"<color=red>{weight}</color>" : weight.ToString();
            string capacity = unlimited ? "∞" : max.ToString();
            return $"<line-height=60%><voffset=-0.12em>{current}</voffset>\n<size=75%>\u2014\u2014</size>\n{capacity}</line-height>";
        }

        // Verified against the game's UpdateInventoryWeight(Player): it rewrites m_weight every inventory update.
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateInventoryWeight))]
        public static class InventoryWeight
        {
            /// <summary>
            /// MP2: the game's "x/y" is not written first while the postfix writes the stacked text; two different
            /// strings a frame made the text mesh rebuild every frame the inventory was open.
            /// </summary>
            [HarmonyPrefix]
            public static bool Prefix(InventoryGui __instance, Player player) => !Writes(__instance, player);

            private static bool Writes(InventoryGui gui, Player player) => InventoryState.Active && player != null && gui.m_weight != null;

            [HarmonyPostfix]
            public static void Postfix(InventoryGui __instance, Player player)
            {
                if (Writes(__instance, player))
                    __instance.m_weight.text = Format(player);
            }
        }
    }
}

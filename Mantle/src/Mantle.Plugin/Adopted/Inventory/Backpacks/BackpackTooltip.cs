using System.Globalization;
using HarmonyLib;

namespace Mantle.Backpacks
{
    /// <summary>
    /// A backpack's tooltip shows its level's numbers (<see cref="BackpackLevels"/>) in the game's own style: the movement
    /// line the game writes for worn gear, and what items in its slots weigh. The quality asked for is the one shown, so
    /// the crafting panel's upgrade shows the next level's. One line per level made once; asked every frame while a pack
    /// is hovered, so nothing but the one join. Appended sub-tooltips are left alone.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    public static class BackpackTooltip
    {
        private static readonly string[] lines = new string[BackpackLevels.Max + 1];

        [HarmonyPostfix]
        public static void Postfix(ItemDrop.ItemData item, int qualityLevel, bool appending, ref string __result)
        {
            if (appending || __result == null || BackpackCatalog.Of(item) == null)
                return;
            int level = UnityEngine.Mathf.Clamp(qualityLevel, 1, BackpackLevels.Max);
            __result += lines[level] ?? (lines[level] = Line(level));
        }

        private static string Line(int level)
        {
            string speed = (BackpackLevels.Speed(level) * 100f).ToString("+0;-0;0", CultureInfo.InvariantCulture);
            string load = (BackpackLevels.Load(level) * 100f).ToString("0", CultureInfo.InvariantCulture);
            return $"\n$item_movement_modifier: <color=orange>{speed}%</color>\n{BackpackWords.Load}: <color=orange>{load}%</color>";
        }
    }
}
using Mantle.Tackle;
using UnityEngine;

namespace Mantle.Backpacks
{
    /// <summary>
    /// Backpack levels (a player's suggestion, the user's call 2026-10-11): every pack is the game's quality 1 to 4,
    /// upgraded at its own station in the game's upgrade tab. A worn pack slows its wearer, less with each level (the
    /// con for what packs add beyond the game), and what lies in its slots weighs less with each level. Fixed numbers, no
    /// setting and no YAML key: each level's upgrade costs a fifth of the pack's own cost, rounded up (another pack in
    /// the cost is not asked again), times the game's own step (<c>Piece.Requirement.GetAmount</c>: 1, 2, 4).
    /// </summary>
    public static class BackpackLevels
    {
        public const int Max = 4;

        private static readonly float[] speed = { -0.08f, -0.06f, -0.04f, -0.02f };
        private static readonly float[] load = { 1f, 0.95f, 0.90f, 0.85f };

        /// <summary>The movement modifier a pack of this level gives while worn, -0.08 at level 1.</summary>
        public static float Speed(int level) => speed[Index(level)];

        /// <summary>What an item in a worn pack's slots weighs, as a share of its weight: 1 at level 1, 0.85 at level 4.</summary>
        public static float Load(int level) => load[Index(level)];

        /// <summary>The worn pack's movement modifier for the local player's frame, 0 with none.</summary>
        public static float WornSpeed(Player player)
        {
            ItemDrop.ItemData pack = Backpack.WornItemNow(player);
            return pack != null ? Speed(pack.m_quality) : 0f;
        }

        /// <summary>The per-level amounts of a pack's recipe, set again whenever its cost is.</summary>
        public static void PriceUpgrades(Piece.Requirement[] cost)
        {
            foreach (Piece.Requirement need in cost)
                need.m_amountPerLevel = IsOwnItem(need.m_resItem) ? 0 : Mathf.CeilToInt(need.m_amount / 5f);
        }

        private static bool IsOwnItem(ItemDrop item) =>
            item != null && (BackpackCatalog.Of(item.m_itemData) != null || TackleboxCatalog.Of(item.m_itemData) != null);

        private static int Index(int level) => Mathf.Clamp(level, 1, Max) - 1;
    }
}
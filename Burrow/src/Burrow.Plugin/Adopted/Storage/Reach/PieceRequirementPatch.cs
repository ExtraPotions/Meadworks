using HarmonyLib;

namespace Burrow.Reach
{
    /// <summary>
    /// Counting for pieces: <c>Player.HaveRequirements(Piece, RequirementMode)</c> returns false for a missing
    /// station, a missing DLC or missing items in one method, so the postfix repeats the station and DLC checks
    /// before it counts inventory plus reachable containers. IsKnown is about known materials and stays vanilla.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
    public static class PieceRequirementPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            if (__result || piece == null || mode == Player.RequirementMode.IsKnown || !ReachRules.Active(ReachMode.Building))
                return;
            if (!StationReady(__instance, piece, mode) || DlcMissing(piece))
                return;
            __result = HaveWithStorage(__instance.GetInventory(), piece, mode);
        }

        private static bool StationReady(Player player, Piece piece, Player.RequirementMode mode)
        {
            if (piece.m_craftingStation == null)
                return true;
            string name = piece.m_craftingStation.m_name;
            if (mode == Player.RequirementMode.CanAlmostBuild)
                return player.m_knownStations.ContainsKey(name);
            if (CraftingStation.HaveBuildStationInRange(name, player.transform.position) != null)
                return true;
            return ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench);
        }

        private static bool DlcMissing(Piece piece)
        {
            return piece.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(piece.m_dlc);
        }

        private static bool HaveWithStorage(Inventory inventory, Piece piece, Player.RequirementMode mode)
        {
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (requirement.m_resItem == null || requirement.m_amount <= 0)
                    continue;
                string name = Requirements.Name(requirement);
                int total = inventory.CountItems(name) + ReachCount.InContainers(name, -1, true);
                int need = mode == Player.RequirementMode.CanAlmostBuild ? 1 : requirement.m_amount;
                if (total < need)
                    return false;
            }
            return true;
        }
    }
}

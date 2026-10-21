namespace Burrow.Reach
{
    /// <summary>The game's own requirement filter, shared by every patch that walks a recipe's resources.</summary>
    internal static class Requirements
    {
        /// <summary>
        /// A requirement the game ignores at this station: upgrader resources at a normal station, normal
        /// resources at an upgrader, upgrader resources without a station, and requirements without an item.
        /// </summary>
        public static bool Skipped(CraftingStation station, Piece.Requirement requirement)
        {
            if (requirement == null || requirement.m_resItem == null)
                return true;
            if (station != null)
                return station.m_upgrader != requirement.m_upgraderResource;
            return requirement.m_upgraderResource;
        }

        public static string Name(Piece.Requirement requirement) => requirement.m_resItem.m_itemData.m_shared.m_name;
    }
}

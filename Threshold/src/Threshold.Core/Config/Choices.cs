// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the choice settings of design section 4. Names are config values; never renumber.
namespace ExtraPotions.Threshold.Config
{
    /// <summary>Where the map's Return To Hub button is offered (PortalSelector <c>ReturnToMain</c>, same values).</summary>
    public enum ReturnToHubMode { Always, UnpairedOnly, Off }

    /// <summary>Who may make a portal a hub or clear it (design B4), checked on the server.</summary>
    public enum HubEditing { Anyone, CreatorOrAdmin, AdminOnly }

    /// <summary>The portal destination interface; Map retains the integrated map and list.</summary>
    public enum TeleportMode { Map, TargetTeleport, Default }

    public enum OreTransportMode { Blocked, StoneToStone, FromStoneToWood, All }

    /// <summary>Order of the picker list inside each category (design A2).</summary>
    public enum SortOrder { Name, Distance }
}

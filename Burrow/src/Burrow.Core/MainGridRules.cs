// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: when the bulk actions may read the player's main grid before Crucible's world id is known (2026-10-29).
namespace ExtraPotions.Burrow
{
    /// <summary>
    /// The bulk actions (quick stack, store all, sort, shared chest takes) read the player's main grid: the inventory
    /// owner's published grid while one lays the inventory out, else PackPanel's or the game's rows. Right after joining a
    /// server Crucible's world id is still unknown. An owner that already lays the inventory out publishes its grid, slot
    /// rows excluded, so it is read as usual; without an owner the game's rows hold. Only an installed owner (Mantle) that
    /// does not lay the inventory out yet may still turn on with the host's settings and claim rows, so the grid stays
    /// empty (nothing moves, nothing is placed) until the world id is known.
    /// </summary>
    public static class MainGridRules
    {
        public static bool WaitsForWorld(bool worldKnown, bool ownerPresent, bool ownerLaysOut) =>
            !worldKnown && ownerPresent && !ownerLaysOut;
    }
}

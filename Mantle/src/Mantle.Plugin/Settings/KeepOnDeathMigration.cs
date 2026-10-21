// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using Mantle.Slots;

namespace Mantle.Settings
{
    /// <summary>
    /// The one-time carry-over from the old Keep Slots On Death switch to the Keep On Death slot groups (f4678b8). The old
    /// switch on kept every slot group, so it maps to all of them; off kept none, which is already Keep On Death's default.
    /// Groups already chosen in Keep On Death win. Once an old entry has been seen, a marker beside it stops the
    /// carry-over from ever running again; the old entry itself is left as it is.
    /// </summary>
    public static class KeepOnDeathMigration
    {
        public const string OldKey = "Keep Slots On Death";
        public const string MarkerKey = "Keep Slots On Death Migrated";

        /// <summary>Every group the old switch kept: gear, backpack, utility, trinket, rings, belt, food, mead, ammo and the tacklebox.</summary>
        public const KeptGroups AllGroups = KeptGroups.Gear | KeptGroups.Backpack | KeptGroups.Utility | KeptGroups.Trinket |
            KeptGroups.Ring | KeptGroups.Belt | KeptGroups.Food | KeptGroups.Mead | KeptGroups.Ammo | KeptGroups.Tacklebox;

        /// <summary>An old entry is there and has not been marked yet.</summary>
        public static bool ShouldMark(string? oldValue, bool migrated) => oldValue != null && !migrated;

        /// <summary>
        /// The groups to set from the old entry's text (as the .cfg holds it), or null when nothing changes: no old entry,
        /// already migrated, the old switch off or unreadable, or groups already chosen.
        /// </summary>
        public static KeptGroups? Plan(string? oldValue, bool migrated, KeptGroups current)
        {
            if (!ShouldMark(oldValue, migrated) || current != KeptGroups.None)
                return null;
            return bool.TryParse(oldValue!.Trim(), out bool kept) && kept ? AllGroups : (KeptGroups?)null;
        }
    }
}

// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the game's Place stacks button leaves favorites in the player inventory.
using System.Collections.Generic;
using System.Reflection;
using Burrow.Stow;
using HarmonyLib;

namespace Burrow.Integration
{
    /// <summary>
    /// The game's Place stacks (<c>Inventory.StackAll</c>, used by the container's button for a chest the player opened)
    /// skips an equipped item. This treats a favorite item, or the content of a favorite slot, of the local player's
    /// inventory the same way, so it stays in the inventory. Every other part of the game's method (which stacks go, the
    /// message, the stat, the change events) is untouched: the only change is that one condition. When a game update
    /// changes the method so that the equipped check cannot be found, the method is left as it is and the log says so once.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll))]
    public static class PlaceStacksFavourites
    {
        private static readonly MethodInfo Equipped = AccessTools.Method(typeof(Humanoid), nameof(Humanoid.IsItemEquiped));
        private static readonly MethodInfo Kept = AccessTools.Method(typeof(PlaceStacksFavourites), nameof(Stays));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int found = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.Calls(Equipped))
                {
                    instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                    instruction.operand = Kept;
                    found++;
                }
            }
            if (found != 1)
                Plugin.Log.LogWarning("Place stacks: the game's equipped check was " + (found == 0 ? "not found" : "found " + found + " times")
                    + "; favorites are not protected from Place stacks in this game version.");
            return code;
        }

        /// <summary>Replaces <c>player.IsItemEquiped(item)</c>: true for an equipped item, as before, and for a favorite of the
        /// local player's inventory.</summary>
        public static bool Stays(Humanoid player, ItemDrop.ItemData item)
        {
            if (player != null && player.IsItemEquiped(item))
                return true;
            Player local = Player.m_localPlayer;
            if (item == null || local == null || player != local || !local.GetInventory().ContainsItem(item))
                return false;
            return Favourites.IsFavouriteItem(item) || Favourites.IsFavouriteSlot(item.m_gridPos);
        }
    }
}

// Adopted from OpenKeep 2.0.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: OpenKeep/OpenKeep/src/Shared/PanelRouting.cs at 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026-10-19: the right-click transfer (parity choice #1) is removed; right click is the
// game's own (use, equip, unequip) everywhere except on a viewed shared chest, where it is refused as before.
using BepInEx.Configuration;
using HarmonyLib;
using Burrow.Core;

namespace Burrow.Shared
{
    /// <summary>
    /// The game's own actions on the chest panel while the local player views a chest it does not own: click
    /// move, drag and drop (the split dialog ends in a drop too), right click, Take all and Stack all. In View
    /// mode every one of them is refused with "Viewing only". In Full mode they go through
    /// <see cref="ChestWriter"/> as requests: a click move from the chest grid takes the stack, from the player
    /// grid puts it, a drop into the chest puts, out of the chest takes into the target slot, within the chest
    /// moves; Take all and Stack all send their batch. The game's local code, which would change an inventory
    /// this client does not own, is skipped every time; the owner keeps the game's code. Right click on a chest
    /// stack (use or equip from the chest) is refused in both modes. The prefixes run first so the other
    /// modules' prefixes on the same methods see a zero amount (Harmony runs every prefix).
    /// </summary>
    public static class PanelRouting
    {
        /// <summary>The viewed container when the local player views one and the panel shows it, else null.</summary>
        internal static Container Viewed()
        {
            Container container = SharedState.ViewedContainer;
            return SharedState.IsViewing(container) ? container : null;
        }

        internal static bool Refuse()
        {
            Messages.Center(SharedWords.ReadOnly);
            return false;
        }

        /// <summary>Stow's Route Modifier click takes this click (its own prefix routes the stack elsewhere); read from the cfg so this module does not depend on Stow.</summary>
        private static bool StowRoutesClick()
        {
            ConfigFile config = Plugin.Instance != null ? Plugin.Instance.Config : null;
            if (config == null)
                return false;
            if (config.TryGetEntry(new ConfigDefinition("2. Stow", "Enabled"), out ConfigEntry<bool> enabled) && !enabled.Value)
                return false;
            return config.TryGetEntry(new ConfigDefinition("2. Stow", "Route Modifier"), out ConfigEntry<KeyboardShortcut> modifier) && Keys.Held(modifier);
        }

        private static bool ChestClick(Container container, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
        {
            if (!SharedState.FullMode)
                return Refuse();
            if (mod == InventoryGrid.Modifier.Move || mod == InventoryGrid.Modifier.Drop)
            {
                ChestWriter.Take(container, item, item.m_stack, null);
                return false;
            }
            return true;
        }

        /// <summary>The game's Ctrl+click on the player grid: the stack goes into the open chest, here as a request.</summary>
        private static bool PlayerMoveClick(Container container, ItemDrop.ItemData item)
        {
            if (!SharedState.FullMode)
                return Refuse();
            if (item.m_shared.m_questItem)
                return false;
            Player player = Player.m_localPlayer;
            player.RemoveEquipAction(item);
            player.UnequipItem(item);
            ChestWriter.Put(container, item, item.m_stack, null, null);
            return false;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
        private static class SelectedPatch
        {
            [HarmonyPriority(Priority.First)]
            [HarmonyPrefix]
            private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, InventoryGrid.Modifier mod)
            {
                Container container = Viewed();
                if (container == null)
                    return true;
                Inventory chest = container.GetInventory();
                if (__instance.m_dragGo != null)
                {
                    bool involvesChest = __instance.m_dragInventory == chest || grid.GetInventory() == chest;
                    return !involvesChest || SharedState.FullMode || Refuse();
                }
                if (item == null)
                    return true;
                if (grid.GetInventory() == chest)
                    return ChestClick(container, item, mod);
                if (grid == __instance.m_playerGrid && mod == InventoryGrid.Modifier.Move && !StowRoutesClick())
                    return PlayerMoveClick(container, item);
                return true;
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnRightClickItem))]
        private static class RightClickPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item)
            {
                Container container = Viewed();
                if (container == null || item == null || grid.GetInventory() != container.GetInventory())
                    return true;
                return Refuse();
            }
        }

        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
        private static class DropPatch
        {
            [HarmonyPriority(Priority.First)]
            [HarmonyPrefix]
            private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, ref int amount, Vector2i pos, ref bool __result)
            {
                Container container = Viewed();
                if (container == null || item == null)
                    return true;
                Inventory chest = container.GetInventory();
                bool toChest = __instance.GetInventory() == chest;
                bool fromChest = fromInventory == chest;
                if (!toChest && !fromChest)
                    return true;
                int wanted = amount;
                amount = 0;
                __result = SharedState.FullMode;
                if (!__result)
                    return Refuse();
                Route(container, item, fromChest, toChest, wanted, pos);
                return false;
            }

            private static void Route(Container container, ItemDrop.ItemData item, bool fromChest, bool toChest, int wanted, Vector2i pos)
            {
                if (toChest && fromChest)
                    ChestWriter.Move(container, item.m_gridPos, pos, wanted, null);
                else if (toChest)
                    ChestWriter.Put(container, item, wanted, pos, null);
                else
                    ChestWriter.TakeTo(container, item, wanted, pos, null);
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTakeAll))]
        private static class TakeAllPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryGui __instance)
            {
                Container container = Viewed();
                if (container == null)
                    return true;
                if (!SharedState.FullMode)
                    return Refuse();
                __instance.SetupDragItem(null, null, 1);
                ChestWriter.TakeAll(container, null);
                return false;
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnStackAll))]
        private static class StackAllPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(InventoryGui __instance)
            {
                Container container = Viewed();
                if (container == null)
                    return true;
                if (!SharedState.FullMode)
                    return Refuse();
                __instance.SetupDragItem(null, null, 1);
                ChestWriter.StackAll(container, null);
                return false;
            }
        }
    }
}

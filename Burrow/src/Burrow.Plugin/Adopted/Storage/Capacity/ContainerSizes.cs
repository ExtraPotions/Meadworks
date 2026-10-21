// Adopted from OpenKeep 2.0.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: OpenKeep/OpenKeep/src/Capacity/ContainerSizes.cs at 31d8ee35ac4ccfd14c83aa930e4b602ac4dfa230.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026-10-10 (parity choice #8): the wanted size of a prefab is resolved from its entry or the chests: line
// against its vanilla size; a buildable chest is a container piece in a hammer table that is no ship and no cart.
// Changed by expDARE, 2026-10-13 (playtest P7): a size that would hide items grows just enough to show them instead of being refused,
// also right after a container loads its items (a chest saved bigger than its prefab's new size), so nothing is ever hidden.
// Changed by expDARE, 2026-10-19: Shepherd feeding troughs (the feed container role) keep their own size; only a trough saved
// bigger by an earlier Burrow grows on load to show its items. A resize saves only an inventory in step with its ZDO.
using System;
using System.Collections.Generic;
using Burrow.Core;
using ExtraPotions.Burrow;
using UnityEngine;

namespace Burrow.Capacity
{
    /// <summary>
    /// Applies the configured sizes to the container prefabs (Container.m_width / m_height, copied into every new
    /// instance) and to every loaded container of those prefabs (the Container fields and its Inventory's
    /// m_width / m_height, which the container grid reads every frame). Prefabs that leave the file or are
    /// switched off go back to vanilla. A container is never shrunk below a row or column that holds an item: it takes the
    /// configured size grown just enough to cover its items.
    /// </summary>
    public static class ContainerSizes
    {
        // Prefab name to the size this module currently keeps it at.
        private static readonly Dictionary<string, ContainerSize> managed = new Dictionary<string, ContainerSize>(StringComparer.OrdinalIgnoreCase);

        public static void ApplyAll()
        {
            if (ZNetScene.instance == null)
                return;
            Dictionary<string, Container> prefabs = ContainerPrefabs.All();
            foreach (KeyValuePair<string, Container> prefab in prefabs)
                VanillaSizes.Remember(prefab.Key, prefab.Value);
            Dictionary<string, ContainerSize> wanted = Wanted(prefabs);
            Dictionary<string, ContainerSize> restore = new Dictionary<string, ContainerSize>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Container> prefab in prefabs)
            {
                ContainerSize vanilla = VanillaSizes.Remember(prefab.Key, prefab.Value);
                if (wanted.TryGetValue(prefab.Key, out ContainerSize size))
                {
                    SetPrefab(prefab.Value, size);
                    managed[prefab.Key] = size;
                }
                else if (managed.Remove(prefab.Key))
                {
                    SetPrefab(prefab.Value, vanilla);
                    restore[prefab.Key] = vanilla;
                }
            }
            foreach (Container container in ContainerScan.All())
                ApplyLoaded(container, restore);
        }

        /// <summary>The sizes the file asks for, by prefab, resolved against each vanilla size; none when the module is off.</summary>
        private static Dictionary<string, ContainerSize> Wanted(Dictionary<string, Container> prefabs)
        {
            Dictionary<string, ContainerSize> wanted = new Dictionary<string, ContainerSize>(StringComparer.OrdinalIgnoreCase);
            ContainersModel model = CapacityModule.Set != null ? CapacityModule.Set.Current as ContainersModel : null;
            if (!CapacitySettings.Enabled.Value || model == null)
                return wanted;
            HashSet<string> buildable = model.Chests.HasValue ? BuildableChests(prefabs) : new HashSet<string>();
            foreach (KeyValuePair<string, Container> prefab in prefabs)
            {
                ContainerSizeSpec? spec = ContainerSizeDefaults.For(prefab.Key, buildable.Contains(prefab.Key), model.Specs, model.Chests,
                    Burrow.Integration.IntegrationLinks.IsFeedContainer(prefab.Value));
                if (!spec.HasValue || !VanillaSizes.TryGet(prefab.Key, out ContainerSize vanilla))
                    continue;
                (int width, int height) = spec.Value.Resolve(vanilla.Width, vanilla.Height);
                wanted[prefab.Key] = new ContainerSize(width, height);
            }
            return wanted;
        }

        /// <summary>
        /// Container prefabs a player can build: pieces of any build table (the hammer's and every mod's tool) that carry a
        /// container and are no ship, no cart and no feeding trough. Dungeon and world chests are in no table, so they keep their size.
        /// </summary>
        public static HashSet<string> BuildableChests(Dictionary<string, Container> prefabs)
        {
            HashSet<string> chests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ObjectDB.instance == null)
                return chests;
            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                PieceTable table = item != null && item.GetComponent<ItemDrop>() is ItemDrop drop ? drop.m_itemData.m_shared.m_buildPieces : null;
                if (table == null || table.m_pieces == null)
                    continue;
                foreach (GameObject piece in table.m_pieces)
                {
                    if (piece == null || !prefabs.ContainsKey(piece.name))
                        continue;
                    if (piece.GetComponentInChildren<Ship>(true) != null || piece.GetComponentInChildren<Vagon>(true) != null)
                        continue;
                    if (Burrow.Integration.IntegrationLinks.IsFeedContainer(prefabs[piece.name]))
                        continue;
                    chests.Add(piece.name);
                }
            }
            return chests;
        }

        /// <summary>The size Burrow keeps the prefab at now, if any (the test bridge and the preview read it).</summary>
        public static bool TryManaged(string prefab, out ContainerSize size) => managed.TryGetValue(prefab, out size);

        /// <summary>The item database woke after the scene: buildable chests are known now, so sizes are applied again.</summary>
        [HarmonyLib.HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class DatabasePatch
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix() => ExtraPotions.Burrow.Embedded.PatchGuard.Guard.Run("container sizes", ApplyAll);
        }

        /// <summary>
        /// Container.Load: the game builds a container's grid from the prefab before its items arrive, and keeps a saved item outside
        /// that grid without showing it. A managed container that loaded such items grows to cover them (no save: the items are unchanged).
        /// So does a feeding trough saved bigger by an earlier Burrow; a trough whose items fit keeps its own size.
        /// </summary>
        [HarmonyLib.HarmonyPatch(typeof(Container), "Load")]
        private static class LoadPatch
        {
            [HarmonyLib.HarmonyPostfix]
            private static void Postfix(Container __instance, bool __result)
            {
                if (__result)
                    ExtraPotions.Burrow.Embedded.PatchGuard.Guard.Run("container sizes", () => CoverLoaded(__instance));
            }
        }

        private static void CoverLoaded(Container container)
        {
            if (container == null || !CapacitySettings.Enabled.Value)
                return;
            if (!managed.ContainsKey(ContainerScan.PrefabName(container)) && !Burrow.Integration.IntegrationLinks.IsFeedContainer(container))
                return;
            Inventory inventory = container.GetInventory();
            if (inventory == null)
                return;
            ContainerSize size = Covering(inventory, new ContainerSize(inventory.GetWidth(), inventory.GetHeight()));
            if (size.Width == inventory.GetWidth() && size.Height == inventory.GetHeight())
                return;
            container.m_width = inventory.m_width = size.Width;
            container.m_height = inventory.m_height = size.Height;
        }

        /// <summary><paramref name="size"/> grown to cover every item of <paramref name="inventory"/>.</summary>
        private static ContainerSize Covering(Inventory inventory, ContainerSize size)
        {
            int lastColumn = -1, lastRow = -1;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                lastColumn = Math.Max(lastColumn, item.m_gridPos.x);
                lastRow = Math.Max(lastRow, item.m_gridPos.y);
            }
            (int width, int height) = ContainerSizeSpec.Cover(size.Width, size.Height, lastColumn, lastRow);
            return new ContainerSize(width, height);
        }

        private static void SetPrefab(Container prefab, ContainerSize size)
        {
            prefab.m_width = size.Width;
            prefab.m_height = size.Height;
        }

        private static void ApplyLoaded(Container container, Dictionary<string, ContainerSize> restore)
        {
            string name = ContainerScan.PrefabName(container);
            if (managed.TryGetValue(name, out ContainerSize size) || restore.TryGetValue(name, out size))
                Resize(container, name, size);
        }

        /// <summary>Container.Awake: a container instantiated before the first apply gets its configured size.</summary>
        public static void ApplyTo(Container container)
        {
            if (container == null || !CapacitySettings.Enabled.Value)
                return;
            string name = ContainerScan.PrefabName(container);
            if (managed.TryGetValue(name, out ContainerSize size))
                Resize(container, name, size);
        }

        private static void Resize(Container container, string name, ContainerSize size)
        {
            Inventory inventory = container.GetInventory();
            if (inventory == null)
                return;
            size = Covering(inventory, size);
            bool same = inventory.GetWidth() == size.Width && inventory.GetHeight() == size.Height
                && container.m_width == size.Width && container.m_height == size.Height;
            if (same)
                return;
            container.m_width = size.Width;
            container.m_height = size.Height;
            inventory.m_width = size.Width;
            inventory.m_height = size.Height;
            // Changed() makes the owner save the inventory to the ZDO. Only an inventory that matches the ZDO's current data may
            // be saved: before the first Container.Load it is still empty, and right after an ownership change it can be older
            // than the previous owner's last save. Either save would overwrite the stored items; the size itself is never saved.
            ZDO zdo = container.m_nview != null && container.m_nview.IsValid() ? container.m_nview.GetZDO() : null;
            if (zdo != null && container.m_lastRevision == zdo.DataRevision)
                inventory.Changed();
        }
    }
}

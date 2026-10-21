// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Mimic
{
    /// <summary>
    /// A dead mimic drops the loot of the chest it pretended to be: that chest's own loot table (the one the game would
    /// have filled it from) is rolled into the creature's drop list, which then goes the way every creature's does - to
    /// its corpse, spilling when the corpse fades. Anything a loot mod adds by the mimic's own name (Epic Loot, given an
    /// entry for ECP_CryptMimic) comes on top; the chest rows themselves are left as rolled.
    /// </summary>
    public static class MimicLoot
    {
        public static void AddChestLoot(CharacterDrop drop, List<KeyValuePair<GameObject, int>> list)
        {
            if (drop == null || list == null || drop.GetComponent<MimicDisguise>() == null)
            {
                return;
            }
            DropTable? table = ChestTable(drop.GetComponent<ZNetView>());
            if (table == null)
            {
                return;
            }
            var counts = new Dictionary<GameObject, int>();
            foreach (GameObject item in table.GetDropList())
            {
                counts[item] = counts.TryGetValue(item, out int n) ? n + 1 : 1;
            }
            foreach (KeyValuePair<GameObject, int> row in counts)
            {
                list.Add(row);
            }
        }

        /// <summary>The chest it replaced (the ZDO remembers which), or the forest crypt chest for one spawned by hand.</summary>
        private static DropTable? ChestTable(ZNetView nview)
        {
            string chest = MimicPrefabs.Chest;
            if (nview != null && nview.IsValid())
            {
                chest = nview.GetZDO().GetString(CryptSwap.ChestKey, chest);
            }
            GameObject? prefab = ZNetScene.instance.GetPrefab(chest) ?? ZNetScene.instance.GetPrefab(MimicPrefabs.Chest);
            Container? container = prefab != null ? prefab.GetComponent<Container>() : null;
            return container != null ? container.m_defaultItems : null;
        }
    }
}

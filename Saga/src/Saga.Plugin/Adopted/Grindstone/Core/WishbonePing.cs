// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/WishbonePing.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The Wishbone's near ping (the effect list of its SE_Finder, m_pingEffectNear: vfx_WishbonePing and
    /// sfx_WishbonePing_near), played at a spot as a local copy on this client only: instantiated with network views
    /// disabled, the way the game makes its build ghost, so no ZDO is made and nobody gets a second, networked copy. A
    /// copy is removed after a few seconds whatever its own timer does. Without a Wishbone in the game it is silent.
    /// <para>The sound is 3D (read from the prefab 2026-09-27: spatial blend 1, full volume within 5 m, fading out to
    /// 25 m), so it is heard from the spot's direction. Shared by the Sailing lookout (<see cref="LookoutPulse"/>: every
    /// client plays it at the ship) and the Pickaxes Echo (<see cref="Echo"/>: the miner's client plays it 4 m towards
    /// the deposit).</para>
    /// </summary>
    internal static class WishbonePing
    {
        private const float Life = 8f;

        private static EffectList ping;
        private static ObjectDB readFrom;

        public static void Play(Vector3 position)
        {
            EffectList effects = Ping();
            if (effects == null || effects.m_effectPrefabs == null)
                return;
            foreach (EffectList.EffectData data in effects.m_effectPrefabs)
            {
                if (data != null && data.m_enabled && data.m_prefab != null)
                    Object.Destroy(LocalCopy(data.m_prefab, position), Life);
            }
        }

        private static GameObject LocalCopy(GameObject prefab, Vector3 position)
        {
            bool was = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try
            {
                return Object.Instantiate(prefab, position, Quaternion.identity);
            }
            finally
            {
                ZNetView.m_forceDisableInit = was;
            }
        }

        /// <summary>The Wishbone's near ping, read once per item database (a relog makes a new one).</summary>
        private static EffectList Ping()
        {
            if (ObjectDB.instance == null || ObjectDB.instance == readFrom)
                return ping;
            readFrom = ObjectDB.instance;
            GameObject wishbone = ObjectDB.instance.GetItemPrefab("Wishbone");
            ItemDrop item = wishbone != null ? wishbone.GetComponent<ItemDrop>() : null;
            SE_Finder finder = item != null ? item.m_itemData.m_shared.m_equipStatusEffect as SE_Finder : null;
            ping = finder != null ? finder.m_pingEffectNear : null;
            return ping;
        }
    }
}

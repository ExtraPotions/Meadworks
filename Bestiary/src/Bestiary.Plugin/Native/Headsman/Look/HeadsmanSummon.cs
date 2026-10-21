// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The skeleton the Executioner raises where its thrown axe broke: a copy of the game's sword-and-shield skeleton
    /// (<see cref="Base"/>) that drops nothing and forms before it moves (<see cref="HeadsmanRising"/>). Raised by the
    /// shatter's owner (<see cref="HeadsmanShatter"/>), at most the settings' number standing near at once.
    /// </summary>
    public static class HeadsmanSummon
    {
        public const string Base = "Skeleton_NoArcher", Name = "ECP_HeadsmanSkeleton";
        private const float Near = 40f;

        private static GameObject? prefab;

        public static GameObject Build(GameObject raised)
        {
            prefab = PrefabBench.Copy(raised, Name);
            CharacterDrop? drops = prefab.GetComponent<CharacterDrop>();
            if (drops != null)
            {
                drops.m_drops = new List<CharacterDrop.Drop>();
            }
            prefab.AddComponent<HeadsmanRising>();
            return prefab;
        }

        /// <summary>
        /// OWNER: a skeleton standing on `floor`, facing back the way the axe flew (`flight`), that begins to form at
        /// `formStart` (the world's clock); none when enough stand near already. Its id, or none.
        /// </summary>
        public static ZDOID Raise(Vector3 floor, Quaternion flight, double formStart)
        {
            if (prefab == null || Standing(floor) >= HeadsmanSettings.Summons)
            {
                return ZDOID.None;
            }
            Quaternion facing = Quaternion.Euler(0f, flight.eulerAngles.y + 180f, 0f);
            GameObject raised = Object.Instantiate(prefab, floor, facing);
            ZDO? zdo = raised.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null)
            {
                return ZDOID.None;
            }
            HeadsmanTime.Keep(zdo, HeadsmanRising.RiseKey, formStart);
            return zdo.m_uid;
        }

        private static int Standing(Vector3 floor) =>
            Character.GetAllCharacters().Count(c => c != null && !c.IsDead() && Utils.GetPrefabName(c.gameObject) == Name
                && Vector3.Distance(c.transform.position, floor) < Near);
    }
}

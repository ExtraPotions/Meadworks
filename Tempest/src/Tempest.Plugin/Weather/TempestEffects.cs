// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Tempest.Integration;
using UnityEngine;

namespace ExtraPotions.Tempest.Runtime
{
    internal static class TempestEffects
    {
        static readonly List<(GameObject Root, int Serial, bool ActiveOnly)> Owned = new List<(GameObject, int, bool)>();

        internal static void Track(GameObject root, bool activeOnly)
        {
            var current = TempestWorld.Current;
            if (root != null && current != null) Owned.Add((root, current.Serial, activeOnly));
        }

        /// <summary>
        /// A copy of a game effect for this peer only. Every peer plays these itself, so a networked prefab
        /// (sfx_Frost_Start, the meteor hits) is made the way the game makes local copies, with its ZNetView disabled;
        /// otherwise each peer's copy becomes a network object that every other peer also draws.
        /// </summary>
        internal static GameObject InstantiateLocal(GameObject prefab, Vector3 at)
        {
            var was = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try { return Object.Instantiate(prefab, at, Quaternion.identity); }
            finally { ZNetView.m_forceDisableInit = was; }
        }

        internal static void Tick()
        {
            var current = TempestWorld.Current;
            var now = TempestWorld.Now;
            var enabled = TempestModules.Active(TempestModule.Events);
            for (var i = Owned.Count - 1; i >= 0; i--)
            {
                var effect = Owned[i];
                if (effect.Root == null) { Owned.RemoveAt(i); continue; }
                if (enabled && current != null && current.Serial == effect.Serial
                    && now < (effect.ActiveOnly ? current.ClearingStart : current.End)) continue;
                Release(effect.Root);
                Owned.RemoveAt(i);
            }
        }

        internal static void Clear()
        {
            foreach (var effect in Owned)
                if (effect.Root != null) Release(effect.Root);
            Owned.Clear();
        }

        static void Release(GameObject root)
        {
            // Only roots returned by Tempest's own Instantiate calls enter this collection.
            foreach (var sound in root.GetComponentsInChildren<ZSFX>(true)) sound.Stop();
            foreach (var audio in root.GetComponentsInChildren<AudioSource>(true)) audio.Stop();
            root.SetActive(false);
            var view = root.GetComponent<ZNetView>();
            if (view != null && view.IsValid())
            {
                // A transferred view may be removed only by its current network owner.
                if (view.IsOwner() && ZNetScene.instance != null) ZNetScene.instance.Destroy(root);
            }
            else Object.Destroy(root);
        }
    }
}

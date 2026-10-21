// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/SeamDriver.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Keeps the local miner's seams moving, once a frame, on the miner's own client: settles a finished swing
    /// (<see cref="SeamSwing"/>) and closes seams whose window ran out or whose chunk or rock is gone
    /// (<see cref="OpenSeams"/>). With Pickaxes turned off or no local player (dead, logged out) every seam closes. A
    /// plain object in the game scene, made on the first pickaxe hit on a multi-chunk rock; the glows hang under it
    /// (<see cref="Holder"/>), so when the game scene closes it takes them along and forgets every seam. A dedicated
    /// server never makes one: it has no local player to swing.
    /// </summary>
    internal sealed class SeamDriver : MonoBehaviour
    {
        private static SeamDriver instance;

        /// <summary>The parent of every seam glow.</summary>
        public static Transform Holder => Ensure().transform;

        public static SeamDriver Ensure()
        {
            if (instance == null)
                instance = new GameObject("saga.seams").AddComponent<SeamDriver>();
            return instance;
        }

        private void Update() => HookGuard.Run("seams", Tick);

        private static void Tick()
        {
            if (!PickSkill.Active || Player.m_localPlayer == null)
            {
                SeamSwing.Forget();
                OpenSeams.CloseAll();
                return;
            }
            if (SeamSwing.Finished)
                SeamSwing.Settle();
            OpenSeams.Tick();
        }

        private void OnDestroy()
        {
            if (instance != this)
                return;
            instance = null;
            SeamSwing.Forget();
            OpenSeams.CloseAll();
        }
    }
}

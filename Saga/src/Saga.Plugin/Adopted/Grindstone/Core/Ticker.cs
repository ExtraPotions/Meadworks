// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Core/Ticker.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// One update for the features that would otherwise give every object of theirs a timer of its own (every loaded
    /// fish, every compost bin): each frame it hands control to each feature's static tick, which tests its own clock
    /// first. A plain object in the game scene, made by the first object that needs it, on any machine (a dedicated
    /// server too); when the game scene closes it goes with it, and the next one makes it again.
    /// </summary>
    internal sealed class Ticker : MonoBehaviour
    {
        private static Ticker instance;

        public static void Ensure()
        {
            if (instance == null)
                instance = new GameObject("saga.ticker").AddComponent<Ticker>();
        }

        public static void Shutdown() { if (instance != null) Object.Destroy(instance.gameObject); instance = null; }

        private void Update()
        {
            FishMark.TickAll();

        }
    }
}

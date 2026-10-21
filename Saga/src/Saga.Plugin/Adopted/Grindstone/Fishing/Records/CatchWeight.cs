// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Records/CatchWeight.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A landed fish's weight on the scale: the game's own weight for its level (2 kg per level for every fish in the
    /// game: its base weight, plus m_scaleWeightByQuality of it per level above 1), give or take 15%, to a tenth of a
    /// kilo. It is the catch's story (the message, the records), not the item's burden: fish stack by ten, so the
    /// inventory keeps the game's weight.
    /// </summary>
    public static class CatchWeight
    {
        private const float Spread = 0.15f;

        public static float Roll(ItemDrop.ItemData.SharedData shared, int level)
        {
            float perLevel = shared.m_scaleWeightByQuality > 0f ? shared.m_scaleWeightByQuality : 1f;
            float weight = Mathf.Max(0.1f, shared.m_weight) * (1f + (Mathf.Max(1, level) - 1) * perLevel);
            return Mathf.Round(weight * Random.Range(1f - Spread, 1f + Spread) * 10f) / 10f;
        }
    }
}

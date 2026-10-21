// Adapted from assets/references/source/ValheimMods-9da28eb5/GrindstoneSkills/GrindstoneSkills/Fishing/BigFish/BigOne.cs at 9da28eb58adbe065be1e4baef779a448acd38e0f.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// "It's a big one!": a fish may grow on the hook. Right after the hook (<see cref="Strike"/>) the angler's client owns
    /// the fish (Fish.OnHooked claims it), so it rolls the big-one chance there: the angler's level (Big One Chance At
    /// 100), times Night Big One Bonus at night (<see cref="FishingConditions"/>). A success raises the fish a level and
    /// rolls again, up to level 5; legendary fish and level 5 fish never grow. The new level goes into the fish's item data and its ZDO, so the fish grows before everyone's
    /// eyes (<see cref="FishMark"/> reloads a hooked fish on the other clients), pulls harder, thrashes longer, and
    /// cleans into more raw fish, all by the game's own rules for its level. Everybody near sees the callout.
    /// </summary>
    public static class BigOne
    {
        /// <summary>The chance, 0..1, that a fish on this cast grows a level.</summary>
        public static float Chance(float level) =>
            Mathf.Clamp01(FishSkill.Share(FishingBigFishSettings.BigOneChanceAt100.Value, level) * FishingConditions.BigOneFactor());

        public static void OnHooked(FishingFloat fishingFloat, FloatFight fight, Fish fish)
        {
            ItemDrop item = FishInfo.Item(fish);
            int level = FishInfo.Level(fish);
            if (item == null || level >= FishInfo.MaxNaturalLevel || !fish.m_nview.IsValid() || !fish.m_nview.IsOwner())
                return;
            float chance = Chance(fight.Level);
            int grown = level;
            while (grown < FishInfo.MaxNaturalLevel && Random.value < chance)
                grown++;
            if (grown == level)
                return;
            item.SetQuality(grown);
            ItemDrop.SaveToZDO(item.m_itemData, fish.m_nview.GetZDO());
            FishCallout.Broadcast(fishingFloat.transform.position + Vector3.up, grown - level > 1 ? SagaText.Packet("gs_e4b861da66d95f2e") : SagaText.Packet("gs_908c0fdb35c10e08"));
        }
    }
}

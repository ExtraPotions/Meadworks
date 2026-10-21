// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/BigFish/LegendaryCatch.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Globalization;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Landing a legendary fish is news: with Announce Legendary Catches on, every player on the server reads "&lt;angler&gt;
    /// landed a legendary Pike (24.1 kg)!" top left (<see cref="FishCallout.Announce"/>). Its guaranteed bonus items are
    /// <see cref="BonusItems"/>' part; its fillets are the game's own count for level 6.
    /// </summary>
    public static class LegendaryCatch
    {
        public static void OnCatch(CatchInfo info)
        {
            if (!info.Legendary || !FishingBigFishSettings.AnnounceLegendary.Value)
                return;
            string weight = info.Weight.ToString("0.0", CultureInfo.InvariantCulture);
            FishCallout.Announce(SagaText.Packet("gs_0551921e5ac18207", info.Player.GetPlayerName(), FishInfo.Item(info.Fish)?.m_itemData.m_shared.m_name ?? info.Name, weight));
        }
    }
}

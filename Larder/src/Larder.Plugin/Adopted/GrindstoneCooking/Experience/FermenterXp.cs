// Larder - ExtraPotions Meadworks food, eating, recovery and kitchen.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
// Adapted from ValheimMods at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: GrindstoneSkills/GrindstoneSkills/Experience/FermenterXp.cs.
// ValheimMods - Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// See THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt and licenses/GPL-3.0.txt.
// Changed by expDARE, 2026: Larder namespace/keys, effective module gates and shared Skills/UI seams;
// native reference interaction, item, food, experience-scope and presentation algorithms retained.
// SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using ExtraPotions.Larder.Cooking;

namespace ExtraPotions.Larder.Runtime.Adopted.GrindstoneCooking
{
    /// <summary>
    /// Cooking experience for tapping a fermenter, which the game gives nothing for. Fermenter.Interact runs on the
    /// tapping player's client: with ward access (PrivateArea.CheckAccess) and the barrel Ready it sends RPC_Tap to
    /// the owner, who empties the barrel and spawns the meads, and returns true. The prefix reads the ready barrel
    /// before the tap empties it (at once when the tapper owns it); the postfix credits the tapper when Interact
    /// reports success. Character history retains each barrel's latest credited start across relogs and barrel
    /// alternation; older ready-state retries remain ineligible. Interaction success is not owner acceptance.
    /// </summary>
    public static class FermenterXp
    {
        /// <summary>A ready barrel as the prefix saw it.</summary>
        public sealed class Tap
        {
            public ZDOID Zdo;
            public long Start;
            public long World;
            public Player Actor;
            public ItemDrop Mead;
        }

        private const string HistoryKey = "larder.fermenterCredits.v1";
        private static Player historyActor;
        private static string historyText;
        private static FermenterCredits history;

        private static FermenterCredits History(Player player)
        {
            player.m_customData.TryGetValue(HistoryKey, out string saved);
            if (!ReferenceEquals(historyActor, player) || historyText != saved)
            {
                historyActor = player;
                historyText = saved;
                FermenterCredits.TryRestore(saved, out history);
            }
            return history;
        }

        [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
        private static class InteractPatch
        {
            [HarmonyPrefix]
            private static void Prefix(Fermenter __instance, Humanoid user, bool hold, out Tap __state) =>
                __state = hold ? null : ReadyTap(__instance, user);

            [HarmonyPostfix]
            private static void Postfix(bool __result, Tap __state)
            {
                if (__result && __state != null)
                    Credit(__state);
            }
        }

        private static Tap ReadyTap(Fermenter fermenter, Humanoid user)
        {
            if (ExperienceSettings.FermenterTap.Value <= 0f || user == null || user != Player.m_localPlayer)
                return null;
            if (!ZNet.instance || ZNet.instance.GetWorld() == null)
                return null;
            if (fermenter.m_nview == null || !fermenter.m_nview.IsValid() || fermenter.GetStatus() != Fermenter.Status.Ready)
                return null;
            if (!PrivateArea.CheckAccess(fermenter.transform.position, 0f, flash: false))
                return null;
            Fermenter.ItemConversion conversion = fermenter.GetItemConversion(fermenter.GetContent());
            ZDO zdo = fermenter.m_nview.GetZDO();
            Tap tap = new Tap { Zdo = zdo.m_uid, Start = zdo.GetLong(ZDOVars.s_startTime),
                World = ZNet.instance.GetWorldUID(), Actor = Player.m_localPlayer, Mead = conversion?.m_to };
            return tap.Mead == null || tap.Zdo == ZDOID.None
                || History(tap.Actor)?.CanCredit(tap.World, tap.Zdo.ToString(), tap.Start) != true ? null : tap;
        }

        /// <summary>Fermenter Tap Experience times the Experience Multiplier and the mead's tier.</summary>
        private static void Credit(Tap tap)
        {
            Player player = Player.m_localPlayer;
            if (player == null || player != tap.Actor || !ZNet.instance || ZNet.instance.GetWorld() == null
                || ZNet.instance.GetWorldUID() != tap.World || ExperienceSettings.FermenterTap.Value <= 0f)
                return;
            var credits = History(player);
            if (credits?.TryCredit(tap.World, tap.Zdo.ToString(), tap.Start) != true)
                return;
            // Save the receipt before raising XP so a reentrant interaction cannot credit the same brew.
            player.m_customData[HistoryKey] = historyText = credits.Save();
            float tier = XpScaling.Tier(tap.Mead.m_itemData);
            XpScaling.RaiseScaled(player, ExperienceSettings.FermenterTap.Value * XpScaling.Multiplier * tier);
        }
    }
}

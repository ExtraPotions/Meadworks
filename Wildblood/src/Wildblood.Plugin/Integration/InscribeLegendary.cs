// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Wildblood boss kills and raid wins offer Inscribe a Legendary item (batch 3).
using System;
using ExtraPotions.Wildblood.Adopted.Raids;
using ExtraPotions.Wildblood.Adopted.Runtime;
using HarmonyLib;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// Calls Inscribe's <c>meadworks.inscribe.legendary_reward</c> (<c>Func&lt;string source, string creaturePrefab,
    /// Vector3 position, bool&gt;</c>) when a Wildblood boss dies for good (its progression settles, on the boss owner) and
    /// when a raid ends won (on the raid owner). Inscribe decides from its host settings whether anything drops. Without
    /// Inscribe, or with an Inscribe that does not offer the capability, nothing happens.
    /// </summary>
    internal static class InscribeLegendary
    {
        internal const string Capability = "meadworks.inscribe.legendary_reward";
        private static readonly System.Version Version = new System.Version(1, 0);

        internal static void Offer(string source, string creaturePrefab, Vector3 position)
        {
            try
            {
                if (CruciblePlugin.Services.Capabilities.TryResolve<Func<string, string, Vector3, bool>>(Capability, Version, out var reward) && reward != null)
                    reward(source, creaturePrefab ?? "", position);
            }
            catch (Exception e)
            {
                Plugin.NativeLogger?.LogWarning("Inscribe legendary reward skipped: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(BossProgress), nameof(BossProgress.Complete))]
    internal static class InscribeLegendaryBossPatch
    {
        private static void Postfix(BossProgress.Death? fact)
        {
            if (fact == null || !fact.Terminal || ZDOMan.instance == null) return;
            ZDO? zdo = ZDOMan.instance.GetZDO(fact.Source);
            if (zdo != null) InscribeLegendary.Offer("boss", fact.Boss, zdo.GetPosition());
        }
    }

    [HarmonyPatch(typeof(RaidFinish), nameof(RaidFinish.End))]
    internal static class InscribeLegendaryRaidPatch
    {
        private static void Prefix(RaidRunner runner, RaidEnd how, out bool __state) =>
            __state = how == RaidEnd.Won && runner != null && runner.IsOwner && runner.State.Running;

        private static void Postfix(RaidRunner runner, bool __state)
        {
            if (__state && runner.State.Ended == RaidEnd.Won) InscribeLegendary.Offer("raid", "", runner.Position);
        }
    }
}

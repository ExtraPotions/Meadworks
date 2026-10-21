// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/Core/CatchHook.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System.Globalization;
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// A fish is landed: FishingFloat.Catch runs on the angler's client when the line is in (the float's step), picks the
    /// fish up (or its item), rolls its bonus item from its extra-drop table into the inventory, counts the game's stats
    /// and returns the centre message ("Caught Pike & Amber"). For the local player:
    /// <list type="bullet">
    /// <item>the prefix captures the catch (<see cref="CatchInfo"/>) and raises the bonus-item odds for this one roll
    /// (<see cref="BonusItems"/>);</item>
    /// <item>the postfix credits experience (<see cref="FishXp"/>), writes the angler's log and records
    /// (<see cref="LogBook"/>), may give the bait back (<see cref="BaitSaver"/>), announces a legendary fish
    /// (<see cref="LegendaryCatch"/>), and adds the weight to the game's message;</item>
    /// <item>the finalizer puts the bonus-item table back, also when the catch threw.</item>
    /// </list>
    /// </summary>
    public static class CatchHook
    {
        [HarmonyPatch(typeof(FishingFloat), nameof(FishingFloat.Catch))]
        private static class Landing
        {
            [HarmonyPrefix]
            private static void Prefix(Fish fish, Character owner, out CatchInfo __state) =>
                __state = FishSkill.Active ? HookGuard.Run("catch", () => Begin(fish, owner), null) : null;

            [HarmonyPostfix]
            private static void Postfix(CatchInfo __state, ref string __result)
            {
                if (__state == null || !Landed(__state))
                    return;
                string message = __result;
                __result = HookGuard.Run("catch", () => Finish(__state, message), message);
            }

            [HarmonyFinalizer]
            private static void Finalizer(CatchInfo __state)
            {
                if (__state != null)
                    HookGuard.Run("catch bonus items", () => BonusItems.Restore(__state));
            }
        }

        private static CatchInfo Begin(Fish fish, Character owner)
        {
            if (fish == null || owner == null || owner != Player.m_localPlayer)
                return null;
            CatchInfo info = new CatchInfo((Player)owner, fish, FloatScope.Current);
            BonusItems.Prepare(info);
            return info;
        }

        /// <summary>
        /// Whether the fish was taken. The game ignores its own pickup's result: with no room the fish stays in the world
        /// (and is let go), while a pickup that took it removed its ZDO at once. Only a fish that was taken counts.
        /// </summary>
        private static bool Landed(CatchInfo info) =>
            info.Fish == null || info.Fish.m_nview == null || !info.Fish.m_nview.IsValid();

        private static string Finish(CatchInfo info, string message)
        {
            HookGuard.Run("catch experience", () => FishXp.OnCatch(info));
            HookGuard.Run("angler's log", () => LogBook.OnCatch(info));
            HookGuard.Run("bait saver", () => BaitSaver.OnCatch(info));
            HookGuard.Run("legendary catch", () => LegendaryCatch.OnCatch(info));
            if (info.Weight <= 0f)
                return message;
            string weight = info.Weight.ToString("0.0", CultureInfo.InvariantCulture);
            return SagaText.Format(info.NewRecord ? "gs_fcc5798c83579166" : "gs_ec5b785e5a5f79c2", message, weight);
        }
    }
}

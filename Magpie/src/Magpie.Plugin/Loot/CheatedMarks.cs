// Magpie - ExtraPotions Meadworks loot handling.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ExtraPotions.Magpie.Items;
using ExtraPotions.Magpie.StepAside;
using HarmonyLib;

namespace ExtraPotions.Magpie.Runtime.Loot
{
    /// <summary>
    /// User decision 2026-10-07 (V6): the game does not save <c>ItemData.m_cheated</c> in a ground item's ZDO, so a cheated
    /// drop reads clean after a reload. After <c>ItemDrop.Save</c> the owner writes <c>magpie.cheated</c> = 1 on a cheated
    /// drop; after <c>ItemDrop.Load</c> (which the game runs on Awake and before every pickup) and
    /// <c>ItemDrop.LoadFromExternalZDO</c> the flag is restored from the marker. Rules in <see cref="CheatedMark"/>; the
    /// marker is only written and read while pickup, merge or loot is on (product switch, module switches, step-aside).
    /// </summary>
    internal static class CheatedMarks
    {
        public static bool Active()
        {
            var on = MagpieModule.None;
            foreach (var module in ModuleStepAside.Modules)
                if ((CheatedMark.Modules & module) != 0 && MagpieRuntime.ModuleOn(module))
                    on |= module;
            return CheatedMark.Active(on);
        }

        public static int Stored(ZDO? zdo) => zdo != null ? zdo.GetInt(MagpieCore.CheatedMarkKey, 0) : 0;

        /// <summary>The item's cheated flag including the marker, for merge identity (a stack is never matched by its clean read).</summary>
        public static bool IsCheated(ItemDrop item, ItemDrop.ItemData data)
        {
            var view = item != null ? item.m_nview : null;
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return CheatedMark.OnLoad(Active(), data.m_cheated, Stored(zdo));
        }

        public static void AfterSave(ItemDrop item)
        {
            var view = item.m_nview;
            var data = item.m_itemData;
            if (view == null || !view.IsValid() || !view.IsOwner() || data == null) return;
            var zdo = view.GetZDO();
            var write = CheatedMark.OnSave(Active(), data.m_cheated, Stored(zdo));
            if (write.HasValue) zdo.Set(MagpieCore.CheatedMarkKey, write.Value);
        }

        public static void AfterLoad(ItemDrop item, ZDO? zdo)
        {
            var data = item.m_itemData;
            if (data == null || zdo == null) return;
            data.m_cheated = CheatedMark.OnLoad(Active(), data.m_cheated, Stored(zdo));
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Save))]
    internal static class CheatedMarkSavePatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDrop __instance)
        {
            try { CheatedMarks.AfterSave(__instance); }
            catch (Exception e) { MagpieRuntime.Log?.Error("Cheated mark save failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Load))]
    internal static class CheatedMarkLoadPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDrop __instance)
        {
            try
            {
                var view = __instance.m_nview;
                if (view != null && view.IsValid()) CheatedMarks.AfterLoad(__instance, view.GetZDO());
            }
            catch (Exception e) { MagpieRuntime.Log?.Error("Cheated mark load failed: " + e); }
        }
    }

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.LoadFromExternalZDO))]
    internal static class CheatedMarkExternalLoadPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ItemDrop __instance, ZDO zdo)
        {
            try { CheatedMarks.AfterLoad(__instance, zdo); }
            catch (Exception e) { MagpieRuntime.Log?.Error("Cheated mark external load failed: " + e); }
        }
    }
}

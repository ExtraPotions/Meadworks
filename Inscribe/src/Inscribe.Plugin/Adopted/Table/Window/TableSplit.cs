// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// "How many?" for taking stones or Essence out of the Rune Table (user 2026-10-07: "shift clicking gems/runes in the
    /// table should bring up that split stack thing so you choose how many to move"): the game's own split dialog
    /// (<see cref="InventoryGui.m_splitDialog"/>: slider, amount, icon, OK and Cancel), limited to what the table holds and
    /// answered here instead of by the game's stack split. The game's Enter key (its <c>OnSplitOk</c>) is taken over and its
    /// Escape (<c>HideSplitDialog</c>) cancels, so a pending answer never reaches a later stack split.
    /// </summary>
    [HarmonyPatch]
    internal static class TableSplit
    {
        private static Action<int>? _accept;

        public static bool Pending => _accept != null;

        public static void Ask(Sprite? icon, string name, int most, Action<int> accept)
        {
            SplitDialog? dialog = InventoryGui.instance != null ? InventoryGui.instance.m_splitDialog : null;
            if (dialog == null || most <= 0 || dialog.IsActive)
            {
                return;
            }
            _accept = accept;
            dialog.UpdateLimits(most, false);
            dialog.UpdateIcon(icon, name);
            dialog.SplitAccepted += Accepted;
            dialog.SplitCanceled += Cancel;
            dialog.SetActive(true);
        }

        public static void Cancel()
        {
            SplitDialog? dialog = InventoryGui.instance != null ? InventoryGui.instance.m_splitDialog : null;
            if (dialog == null || _accept == null)
            {
                return;
            }
            _accept = null;
            dialog.SplitAccepted -= Accepted;
            dialog.SplitCanceled -= Cancel;
            dialog.SetActive(false);
        }

        private static void Accepted()
        {
            Action<int>? accept = _accept;
            int amount = (int)InventoryGui.instance.m_splitDialog.m_splitSlider.value;
            Cancel();
            accept?.Invoke(amount);
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSplitOk))]
        [HarmonyPrefix]
        private static bool EnterPressed()
        {
            if (!Pending)
            {
                return true;
            }
            Accepted();
            return false;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.HideSplitDialog))]
        [HarmonyPostfix]
        private static void EscapePressed() => Cancel();
    }
}

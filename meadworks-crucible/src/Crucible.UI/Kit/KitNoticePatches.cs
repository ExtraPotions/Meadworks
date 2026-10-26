// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.ShowMessage), new[] {
        typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(Sprite), typeof(bool), typeof(bool) })]
    static class KitNoticePatches
    {
        [HarmonyPriority(Priority.Last)]
        static void Prefix(MessageHud.MessageType __0, string __1, ref Sprite __3, bool __4, bool __runOriginal)
        {
            if (!__runOriginal || !MeadworksHeadless.AllowsUi || !CrucibleUiPlugin.Active ||
                __0 != MessageHud.MessageType.TopLeft || __3 != null || (Hud.IsUserHidden() && !__4)) return;
            // Keep native queues, duplicate amounts, localization, journal, center position and other patches.
            __3 = KitNoticeIcons.Find(__1);
        }
    }
}

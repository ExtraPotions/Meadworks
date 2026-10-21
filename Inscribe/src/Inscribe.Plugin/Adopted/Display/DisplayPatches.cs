// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The small hooks that keep display state current. Every one runs on the local machine only and sends nothing.
    /// </summary>
    internal static class DisplayPatches
    {
        /// <summary>
        /// A dropped item started (display.md section 5): by <c>Start</c> both locally dropped and remote items hold
        /// their final data. Only networked items count (<c>ZNetView</c> valid), which excludes the game's temporary
        /// inventory instances and inactive prefab clones. The glow manager picks it up on its next, early, tick.
        /// Every client with graphics; the manager does not exist on a headless server.
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop), "Start")]
        private static class ItemDropStartPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ItemDrop __instance)
            {
                GlowManager? manager = GlowManager.Instance;
                if (manager != null && __instance.m_nview != null && __instance.m_nview.IsValid())
                {
                    manager.RequestTick();
                }
            }
        }
    }
}

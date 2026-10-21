// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Look/ArtWarmup.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: gated on the kit look and the C10 headless guard instead of
// PackPanel's master switch; the private FejdStartup.Start is named by string (no publicizer).
using HarmonyLib;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// <c>FejdStartup.Start()</c> postfix (private; the main menu, once per game start, before any world): the kit's
    /// images are decoded here (<see cref="KitArt.Prewarm"/>), where a short stall is hidden by the menu loading, instead
    /// of when a panel first opens. Skipped with the look off or headless; a failure only logs, the images then load on
    /// first use as before.
    /// </summary>
    [HarmonyPatch(typeof(FejdStartup), "Start")]
    public static class ArtWarmup
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (!KitLook.On || !MeadworksHeadless.AllowsUi)
                return;
            try
            {
                KitArt.Prewarm();
            }
            catch (System.Exception e)
            {
                CrucibleUiPlugin.Log?.Warning($"Crucible.UI: could not prepare the panel images early: {e.Message}");
            }
        }
    }
}

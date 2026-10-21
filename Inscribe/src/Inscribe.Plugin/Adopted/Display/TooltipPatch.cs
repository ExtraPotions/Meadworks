// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Runtime;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// Places identification before the vanilla details and the affix block after them (display.md section 3): a postfix on the static
    /// <c>ItemData.GetTooltip(ItemData, int, bool, float, int, bool)</c>, skipped for appended sub-tooltips.
    /// Covers the inventory and container grids, the trader and the radial menu. The crafting
    /// panel passes the recipe prefab's data: while it describes an upgrade, <see cref="CraftingPanel.Shown"/> swaps in
    /// the player's item being upgraded, so its block appears under the recipe (display.md section 2).
    /// Runs on the viewing client, every frame while an item is hovered: one cache read, one block lookup, and a
    /// two-slot memo so the concatenation is not redone while the vanilla text stays the same.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip),
        new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(bool) })]
    internal static class TooltipPatch
    {
        private static readonly string?[] LastVanilla = new string?[2];
        private static readonly string?[] LastBlock = new string?[2];
        private static readonly string?[] LastSummary = new string?[2];
        private static readonly string?[] LastCrafter = new string?[2];
        private static readonly string?[] LastResult = new string?[2];
        private static int _next;

        /// <summary>Runs after Epic Loot's postfix, which cuts a set item's tooltip at the set effect line (and so our block).</summary>
        [HarmonyPostfix, HarmonyPriority(Priority.Last), HarmonyAfter("randyknapp.mods.epicloot")]
        private static void Postfix(ItemDrop.ItemData item, int __1, bool crafting, bool appending, ref string __result)
        {
            if (appending || item == null || __result == null || !InscribeModules.Active(InscribeModule.Display))
            {
                return;
            }
            ItemDrop.ItemData shown = crafting ? CraftingPanel.Shown(item) : item;
            ItemState state = InscribeModules.StepAside.RarityDisplay ? ItemState.Read(shown) : ItemState.Empty;
            string block = state.IsEmpty ? "" : DisplayCache.Block(state, shown);
            string summary = crafting ? ItemIdentification.SummaryForQuality(shown, __1) : ItemIdentification.Summary(shown);
            if (summary.Length > 0 || block.Length > 0)
            {
                __result = Join(__result, summary, block, "");
            }
        }

        /// <summary>Reuses the last two assembled tooltip bodies while their inputs stay unchanged.</summary>
        private static string Join(string vanilla, string summary, string block, string crafter)
        {
            for (int i = 0; i < LastResult.Length; i++)
            {
                if (ReferenceEquals(LastBlock[i], block) && ReferenceEquals(LastSummary[i], summary)
                    && string.Equals(LastCrafter[i], crafter) && string.Equals(LastVanilla[i], vanilla))
                {
                    return LastResult[i]!;
                }
            }
            int slot = _next;
            _next = (_next + 1) % LastResult.Length;
            LastVanilla[slot] = vanilla;
            LastBlock[slot] = block;
            LastSummary[slot] = summary;
            LastCrafter[slot] = crafter;
            LastResult[slot] = (summary.Length > 0 ? summary + "\n\n" : "") + vanilla + block + crafter;
            return LastResult[slot]!;
        }
    }
}

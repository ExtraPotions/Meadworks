using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;
using Mantle.Look;
using Mantle.Ring;
using Mantle.Slots;
using Mantle.Tackle;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// After the game draws a grid (<c>InventoryGrid.UpdateGui</c>). The game makes one element per cell of the whole
    /// inventory, slot rows included, and remakes them all when the size changes; every time it does, the elements of
    /// the slot cells move to the slot panel with their captions (the ring cells to the key ring's pop-up), the cells unused by the
    /// last slot row are hidden, the hotbar numbers stop at 8 and every cell gets the skin. Each frame the grid's root is
    /// kept to the main rows (the game sizes it for every row), the gamepad's selection turns to its slot tab, the stat
    /// sheet is brought up to date, the purse shows its count and the key ring's pop-up and button are brought up to
    /// date. The container grid only gets the skin.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    public static class SlotElements
    {
        private static InventoryElement placedFirst;
        private static int placedCount;
        private static InventoryElement containerFirst;
        private static int containerVersion;
        private static int placedVersion;
        private static int version;
        private static int coinsCount = -1;
        private static string coinsText;

        /// <summary>A setting the elements show changed (labels, keys, skin, layout): place them again next frame.</summary>
        public static void Invalidate() => version++;

        /// <summary>
        /// The game's language changed: every caption, tab, stat line and tooltip is written again at once, not on the
        /// next drawn frame, so nothing in the old language is ever shown.
        /// </summary>
        public static void Relabel(InventoryGui gui)
        {
            Invalidate();
            if (gui != null && gui.m_playerGrid != null && gui.m_playerGrid.m_elements.Count > 0)
                KeepPlayer(gui, gui.m_playerGrid);
        }

        [HarmonyPostfix]
        public static void Postfix(InventoryGrid __instance)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null)
                return;
            if (__instance == gui.m_containerGrid)
                SkinContainer(__instance);
            else if (__instance == gui.m_playerGrid)
                KeepPlayer(gui, __instance);
        }

        private static void KeepPlayer(InventoryGui gui, InventoryGrid grid)
        {
            List<InventoryElement> elements = grid.m_elements;
            if (elements.Count == 0)
                return;
            if (elements[0] != placedFirst || elements.Count != placedCount || version != placedVersion)
            {
                placedFirst = elements[0];
                placedCount = elements.Count;
                placedVersion = version;
                ElementPlacer.PlaceAll(gui, grid);
            }
            PanelDress.Update(gui);
            if (!InventoryState.Active)
                return;
            // The game's own call already used this height (GridRootHeight); written here only if that patch is missing.
            float rows = GridRootHeight.For(grid, grid.m_gridRoot.rect.height);
            if (Mathf.Abs(grid.m_gridRoot.rect.height - rows) > 0.01f)
                grid.m_gridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rows);
            SlotTabs.FollowGamepad(grid);
            GearStats.Refresh();
            ShowCoins(ElementPlacer.PurseElement);
            SlotLabels.Refresh();
            KeyRingPopup.Refresh(gui);
            KeyRingButton.Refresh(gui);
            TacklePopup.Refresh(gui);
            TackleboxSlot.Refresh(ElementPlacer.TackleboxElement);
        }

        /// <summary>The purse shows its count alone ("51"), not the game's "51/999".</summary>
        private static void ShowCoins(InventoryElement purse)
        {
            string text = CoinsText(purse);
            if (text != null)
                purse.m_amount.text = text;
        }

        /// <summary>The purse's count, its text made only when it changes; null without a purse or coins.</summary>
        private static string CoinsText(InventoryElement purse)
        {
            ItemDrop.ItemData coins = purse != null ? InventoryState.ItemIn(SlotKind.Purse, 1) : null;
            if (coins == null)
                return null;
            if (coins.m_stack != coinsCount)
            {
                coinsCount = coins.m_stack;
                coinsText = coinsCount.ToString();
            }
            return coinsText;
        }

        /// <summary>
        /// The game writes every cell's "51/999" each frame and <see cref="ShowCoins"/> wrote "51" back after it, so the
        /// purse's label changed twice a frame and the inventory's canvas was rebuilt every frame it was open (playtest
        /// 2026-10-20, item 25). The game's own write now gives the purse its count, so the text stays the same between
        /// frames; every other cell and grid keeps the game's text.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        public static class PurseAmount
        {
            private static readonly System.Reflection.MethodInfo setText = AccessTools.PropertySetter(typeof(TMPro.TMP_Text), nameof(TMPro.TMP_Text.text));
            private static readonly System.Reflection.MethodInfo written = AccessTools.Method(typeof(PurseAmount), nameof(Write));

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    // label.text = value becomes Write(label, value): the same two values on the stack, labels kept.
                    if (setText != null && instruction.Calls(setText))
                    {
                        instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                        instruction.operand = written;
                    }
                    yield return instruction;
                }
            }

            /// <summary>Stands in for the game's <c>label.text = text</c> in <c>InventoryGrid.UpdateGui</c>.</summary>
            public static void Write(TMPro.TMP_Text label, string text)
            {
                InventoryElement purse = ElementPlacer.PurseElement;
                if (purse != null && ReferenceEquals(label, purse.m_amount) && InventoryState.Active)
                    text = CoinsText(purse) ?? text;
                label.text = text;
            }
        }

        /// <summary>
        /// MP2: the game ends <c>UpdateGui</c> by sizing the grid's root for every inventory row, and the postfix then sized
        /// it back to the main rows, so the root changed size twice every frame the inventory was open and the screen's
        /// layout and canvas were rebuilt each frame. Here the game's own call gets the main rows' height for the player
        /// grid while Mantle lays it out, so the root keeps one size; every other grid and call is untouched.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
        public static class GridRootHeight
        {
            private static readonly System.Reflection.MethodInfo setSize = AccessTools.Method(typeof(RectTransform), nameof(RectTransform.SetSizeWithCurrentAnchors));
            private static readonly System.Reflection.MethodInfo sized = AccessTools.Method(typeof(GridRootHeight), nameof(SetSize));

            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.Calls(setSize))
                    {
                        // The call becomes "ldarg.0" (the grid, keeping any labels) followed by a call to SetSize.
                        instruction.opcode = System.Reflection.Emit.OpCodes.Ldarg_0;
                        instruction.operand = null;
                        yield return instruction;
                        yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, sized);
                        continue;
                    }
                    yield return instruction;
                }
            }

            /// <summary>Stands in for the game's <c>m_gridRoot.SetSizeWithCurrentAnchors(axis, size)</c>.</summary>
            public static void SetSize(RectTransform root, RectTransform.Axis axis, float size, InventoryGrid grid)
            {
                if (axis == RectTransform.Axis.Vertical)
                    size = For(grid, size);
                root.SetSizeWithCurrentAnchors(axis, size);
            }

            /// <summary>The player grid's root holds only the main rows while Mantle lays it out; otherwise the game's size.</summary>
            public static float For(InventoryGrid grid, float gameSize)
            {
                InventoryGui gui = InventoryGui.instance;
                return gui != null && grid == gui.m_playerGrid && InventoryState.Active
                    ? InventoryState.Layout.MainRows * grid.m_elementSpace
                    : gameSize;
            }
        }

        private static void SkinContainer(InventoryGrid grid)
        {
            List<InventoryElement> elements = grid.m_elements;
            if (elements.Count == 0 || (elements[0] == containerFirst && version == containerVersion))
                return;
            containerFirst = elements[0];
            containerVersion = version;
            foreach (InventoryElement element in elements)
                GridSkin.Cell(element);
        }
    }
}

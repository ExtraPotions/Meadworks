using System.Text;
using Mantle.Core;
using Mantle.Look;
using Mantle.Panels;
using Mantle.Slots;
using TMPro;
using UnityEngine;

namespace Mantle.Ring
{
    /// <summary>
    /// The key ring's button on the purse's row of the slot panel (<see cref="SlotPanelLayout.RingCell"/>): a copy of the
    /// game's cell, not a cell of the inventory, so it never holds an item. It shows the ring's bronze icon with its
    /// caption like an empty slot (<see cref="SlotLabels"/>; the icon alone with Slot Labels off) and, in the game's small
    /// corner number, how many different keys the player holds anywhere in the inventory. Hovering lists them with their
    /// counts; a click goes to <see cref="KeyRingClicks"/>. A key never carried before makes it glow, with a note under it
    /// (<see cref="KeyRingNotice"/>). The gamepad selects it through the first ring cell while the
    /// pop-up is shut (<see cref="KeyRingGamepad"/>), and its highlight shows then.
    /// </summary>
    public static class KeyRingButton
    {
        public const string Name = "Mantle_keyring_button";
        private static InventoryElement button;
        private static string shownList;
        private static int[] counts = new int[0];
        private static int[] listed;
        private static int shownKinds = -1;
        private static string kindsText;

        public static void Place(InventoryGui gui, RectTransform panel, SlotPanelLayout plan, float step, TMP_FontAsset font)
        {
            Transform found = panel != null ? panel.Find(Name) : null;
            if (panel == null || plan?.RingCell == null)
            {
                found?.gameObject.SetActive(false);
                button = null;
                return;
            }
            button = found != null ? found.GetComponent<InventoryElement>() : Make(gui, panel);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = SlotPanel.CellPosition(plan.RingCell.Value, step);
            button.gameObject.SetActive(true);
            GridSkin.Cell(button);
            SlotLabels.Set(button, new Slot(SlotKind.Key, 0), font);
            shownList = null;
        }

        /// <summary>Every frame the grid is drawn: the icon, the number of different keys held, the list and the gamepad's highlight.</summary>
        public static void Refresh(InventoryGui gui)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !KeyRing.Active)
                return;
            button.m_icon.enabled = !InventorySettings.SlotLabels.Value;
            button.m_icon.sprite = SlotIcons.For(SlotKind.Key);
            button.m_icon.color = SlotIcons.Hint;
            counts = KeyRing.Counts(InventoryState.Player.GetInventory(), counts);
            int kinds = 0;
            for (int number = 1; number < counts.Length; number++)
                kinds += counts[number] > 0 ? 1 : 0;
            button.m_quality.enabled = kinds > 0;
            if (kinds != shownKinds)
            {
                shownKinds = kinds;
                kindsText = kinds.ToString();
            }
            button.m_quality.text = kindsText;
            ShowList(gui, counts);
            button.m_selected.SetActive(KeyRingGamepad.OnButton(gui));
            KeyRingNotice.Show(button);
        }

        /// <summary>The tooltip: every key held and how many, in ring order; written only when it changed.</summary>
        private static void ShowList(InventoryGui gui, int[] counts)
        {
            // Every frame: the list is written again only when a count changed (or the button was placed again).
            if (shownList != null && KeyRing.SameCounts(counts, listed))
                return;
            listed = (int[])counts.Clone();
            StringBuilder text = new StringBuilder();
            for (int number = 1; number < counts.Length; number++)
            {
                ItemDrop.ItemData key = counts[number] > 0 ? KeyRing.Key(number) : null;
                if (key != null)
                    text.Append(text.Length > 0 ? "\n" : "").Append(key.m_shared.m_name).Append(": <color=orange>").Append(counts[number]).Append("</color>");
            }
            string list = text.Length > 0 ? text.ToString() : Words.NoKeysHeld;
            if (list == shownList)
                return;
            shownList = list;
            button.m_tooltip.Set(Words.KeyRing, list, gui.m_playerGrid.m_tooltipAnchor);
        }

        private static InventoryElement Make(InventoryGui gui, RectTransform panel)
        {
            GameObject go = Object.Instantiate(gui.m_playerGrid.m_elementPrefab, panel);
            go.name = Name;
            InventoryElement element = go.GetComponent<InventoryElement>();
            element.Initialize(-1, -1);
            Blank(element);
            UIInputHandler input = go.GetComponentInChildren<UIInputHandler>();
            input.m_onLeftDown += handler => KeyRingClicks.OnButton(InventoryGui.instance);
            return element;
        }

        /// <summary>Everything an item would show switched off: the game only does that for its own grid's cells.</summary>
        private static void Blank(InventoryElement element)
        {
            element.m_amount.enabled = false;
            element.m_quality.enabled = false;
            element.m_equiped.enabled = false;
            element.m_queued.enabled = false;
            element.m_noteleport.enabled = false;
            element.m_food.enabled = false;
            element.m_durability.gameObject.SetActive(false);
            element.m_selected.SetActive(false);
            Transform binding = element.transform.Find("binding");
            TMP_Text text = binding != null ? binding.GetComponent<TMP_Text>() : null;
            if (text != null)
                text.enabled = false;
        }
    }
}

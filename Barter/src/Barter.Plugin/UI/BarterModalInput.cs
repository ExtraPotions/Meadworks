// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Barter.Runtime
{
    // KitWindow owns window lifetime; Barter owns and consumes its modal controls.
    internal sealed class BarterModalInput
    {
        private readonly KitWindow window;
        private readonly Dictionary<GridCell, Selectable> controls = new Dictionary<GridCell, Selectable>();
        private GridCell? focus;
        private int width, height;

        internal BarterModalInput(KitWindow window) { this.window = window; }

        internal void Add(Selectable control, int column, int row)
        {
            if (control == null) return;
            controls[new GridCell(column, row)] = control;
            width = System.Math.Max(width, column + 1);
            height = System.Math.Max(height, row + 1);
        }

        internal void Reset() { focus = null; }

        internal void Tick()
        {
            if (!window.IsOpen || !MeadworksInput.GamepadActive) return;
            if (KitTextField.Typing) return;
            // Y belongs to this modal even when it has no action here.
            if (MeadworksInput.PadDown(PadButton.Y)) MeadworksInput.PadConsume(PadButton.Y);
            foreach (PadButton button in KitPad.Polled)
            {
                if (!MeadworksInput.PadDown(button)) continue;
                MeadworksInput.PadConsume(button);
                if (button == PadButton.B) { window.Close(); return; }
                SyncFocus();
                if (button == PadButton.A)
                {
                    if (focus.HasValue && controls.TryGetValue(focus.Value, out Selectable selected) && Usable(selected) && selected is Button action)
                        action.onClick.Invoke();
                    return;
                }
                if (width == 0 || height == 0) return;
                var grid = new GridNavigator(width, height, cell => controls.TryGetValue(cell, out Selectable control) && Usable(control));
                focus = focus.HasValue ? KitPad.Next(grid, focus.Value, button) : KitPad.Start(grid);
                if (focus.HasValue && EventSystem.current != null && controls.TryGetValue(focus.Value, out Selectable next))
                    EventSystem.current.SetSelectedGameObject(next.gameObject);
                return;
            }
        }

        private void SyncFocus()
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            foreach (var pair in controls)
                if (Usable(pair.Value) && pair.Value.gameObject == selected) { focus = pair.Key; return; }
            focus = null;
        }

        private static bool Usable(Selectable control) => control != null && control.gameObject.activeInHierarchy && control.IsInteractable();
    }
}

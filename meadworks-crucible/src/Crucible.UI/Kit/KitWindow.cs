using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExtraPotions.Meadworks.Crucible.UI.WindowInput;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// A free-standing kit window (a <see cref="KitPanel"/>) that behaves like the game's own windows while open, through
    /// the adopted <see cref="GameWindow"/> patches (cursor free, no attacks or camera turning, no zoom, Esc closes it)
    /// and the C07 input service (<see cref="MeadworksInput"/>: gameplay hotkeys blocked, or every hotkey while a typing
    /// window is open). An optional hotkey toggles it; with a gamepad the D-pad moves the focus between the controls the
    /// product placed on the window's grid (skipping hidden and disabled ones), A presses, B closes (<see cref="KitPad"/>).
    /// </summary>
    public sealed partial class KitWindow
    {
        private static readonly List<KitWindow> windows = new List<KitWindow>();
        private static bool registered;

        private readonly Dictionary<GridCell, Selectable> controls = new Dictionary<GridCell, Selectable>();
        private readonly bool typing;
        private ConfigEntry<KeyboardShortcut> hotkey;
        private InputContext hotkeyContext = InputContext.Gameplay;
        private GridNavigator navigator;
        private GridCell? focus;

        public string Id { get; }
        public RectTransform Panel { get; }
        public bool IsOpen => Panel != null && Panel.gameObject.activeSelf;
        public bool HideHud { get; set; } = true;

        public event Action Opened;
        public event Action Closed;

        private KitWindow(string id, RectTransform panel, bool typing)
        {
            Id = id;
            Panel = panel;
            this.typing = typing;
        }

        /// <summary>
        /// A closed window of <paramref name="size"/> on the front GUI layer (or under <paramref name="parent"/>); null
        /// headless. <paramref name="typing"/>: the window edits text, so no hotkey fires while it is open.
        /// </summary>
        public static KitWindow Create(string id, Vector2 size, Transform parent = null, bool typing = false, bool round = false)
        {
            RectTransform panel = KitPanel.Create(parent ?? KitCanvas.Front, "Crucible_window_" + id, size, round);
            if (panel == null)
                return null;
            panel.gameObject.SetActive(false);
            // The forged set: calm wood, a straight edge and the forged-iron frame; a round window keeps its disc.
            if (!round)
                KitForged.Window(panel);
            KitWindow window = new KitWindow(id, panel, typing);
            windows.Add(window);
            Register();
            return window;
        }

        /// <summary>The hotkey that opens and closes the window, checked in <paramref name="context"/> while closed.</summary>
        public void Toggle(ConfigEntry<KeyboardShortcut> entry, InputContext context = InputContext.Gameplay)
        {
            hotkey = entry;
            hotkeyContext = context;
        }

        /// <summary>A control the gamepad can reach, at its place on the window's grid (x right, y down).</summary>
        public void Focusable(Selectable control, int x, int y)
        {
            if (control == null)
                return;
            controls[new GridCell(x, y)] = control;
            navigator = null;
        }

        /// <summary>Remove the exact owned control when a pooled row is rebound or disposed.</summary>
        public void Unfocusable(Selectable control)
        {
            foreach(var cell in controls.Where(pair=>pair.Value==control).Select(pair=>pair.Key).ToArray())
            {
                controls.Remove(cell);
                if(focus.HasValue&&focus.Value.Equals(cell))focus=null;
            }
            navigator=null;
        }

        public void Open()
        {
            if (Panel == null || IsOpen)
                return;
            Panel.gameObject.SetActive(true);
            Panel.SetAsLastSibling();
            focus = null;
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            Panel.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        public void Destroy()
        {
            windows.Remove(this);
            if (Panel != null)
                UnityEngine.Object.Destroy(Panel.gameObject);
        }

        /// <summary>Plugin Update: hotkeys and gamepad for every window.</summary>
        internal static void Tick()
        {
            for (int i = windows.Count - 1; i >= 0; i--)
            {
                KitWindow window = windows[i];
                if (window.Panel == null)
                {
                    windows.RemoveAt(i);
                    continue;
                }
                window.Update();
            }
        }

        private void Update()
        {
            if (hotkey != null && MeadworksInput.Pressed(hotkey, IsOpen ? InputContext.Window : hotkeyContext))
            {
                if (IsOpen) Close(); else Open();
                return;
            }
            if (!IsOpen || !MeadworksInput.GamepadActive)
                return;
            foreach (PadButton button in KitPad.Polled)
            {
                if (!MeadworksInput.PadDown(button))
                    continue;
                Pad(button);
                return;
            }
        }

        private void Pad(PadButton button)
        {
            SyncSelectedFocus();
            switch (KitPad.ActionOf(button))
            {
                case KitPadAction.Close:
                    MeadworksInput.PadConsume(button);
                    Close();
                    return;
                case KitPadAction.Activate:
                    if (focus.HasValue && controls.TryGetValue(focus.Value, out Selectable pressed) && Usable(pressed) && pressed is Button clickable)
                        clickable.onClick.Invoke();
                    return;
                case KitPadAction.Move:
                    GridNavigator grid = Navigator();
                    if (grid == null)
                        return;
                    focus = focus.HasValue ? KitPad.Next(grid, focus.Value, button) : KitPad.Start(grid);
                    if (focus.HasValue && EventSystem.current != null && controls.TryGetValue(focus.Value, out Selectable selected))
                        EventSystem.current.SetSelectedGameObject(selected.gameObject);
                    return;
            }
        }

        private void SyncSelectedFocus()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null)
                foreach (var pair in controls)
                    if (Usable(pair.Value) && pair.Value.gameObject == selected)
                    {
                        focus = pair.Key;
                        return;
                    }
            focus = null;
        }

        private GridNavigator Navigator()
        {
            if (navigator != null)
                return navigator;
            if (controls.Count == 0)
                return null;
            int width = 0, height = 0;
            foreach (GridCell cell in controls.Keys)
            {
                width = Math.Max(width, cell.X + 1);
                height = Math.Max(height, cell.Y + 1);
            }
            return navigator = new GridNavigator(width, height, c => controls.TryGetValue(c, out Selectable control) && Usable(control));
        }

        private static bool Usable(Selectable control) => control != null && control.gameObject.activeInHierarchy && control.IsInteractable();

        // Shown on screen, not just active: an editor left active inside a parent window that closed (KitAlmanacDialog) must
        // not keep the cursor, the input contexts or KitWindows.AnyOpen on.
        private static bool AnyOpen(bool typingWindows)
        {
            foreach (KitWindow window in windows)
                if (window.Panel != null && window.Panel.gameObject.activeInHierarchy && window.typing == typingWindows)
                    return true;
            return false;
        }

        private static void CloseAll()
        {
            foreach (KitWindow window in windows.ToArray())
                window.Close();
        }

        private static void Register()
        {
            if (registered)
                return;
            registered = true;
            GameWindow.Add(() => AnyOpen(false) || AnyOpen(true), CloseAll, HasHudBlockingWindow);
            MeadworksInput.AddModWindow(() => AnyOpen(false));
            MeadworksInput.AddTypingWindow(() => AnyOpen(true));
        }

        private static bool HasHudBlockingWindow()
        {
            foreach (KitWindow window in windows)
                if (window.HideHud && window.Panel != null && window.Panel.gameObject.activeInHierarchy)
                    return true;
            return false;
        }
    }
}

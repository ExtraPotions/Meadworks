// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original. The pause-menu attachment and navigation splice follow the ownership baseline the products used
// before (Cairn, Hex, Thing, Beacon, Almanac), now in one place; the buttons use the kit's GameButtons/KitButton.
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// The single "MeadWorks" entry of the game's pause menu. It is drawn while at least one product has registered a settings
    /// window with <see cref="MeadworksMenu"/> and that entry's gate is open; it opens a small chooser listing them. Products
    /// no longer add pause-menu buttons of their own. The MeadWorks menu key (<see cref="MeadworksMenuKey"/>, F8 by default) opens
    /// the same chooser from anywhere in game.
    /// </summary>
    public static class MeadworksPauseMenu
    {
        const string ButtonName = "Meadworks_Menu";
        const float ChooserWidth = 640f;
        const float RowHeight = 36f;
        const float Gap = 6f;
        const float Recheck = 0.25f;

        static Menu owner;
        static Button button;
        static KitNativeMenus.EntryLook look;
        static bool closingGameMenu;
        static KitWindow chooser;
        static string drawn = "";
        static string product = "";
        static int listed;
        static float nextCheck, nextFingerprint;
        static readonly HashSet<Selectable> navigationVisited = new HashSet<Selectable>();
        static string drawnProduct = "";
        static int drawnWidth, drawnHeight;
        static readonly Dictionary<Selectable, Navigation> links = new Dictionary<Selectable, Navigation>();
        static readonly List<Button> rows = new List<Button>();
        static readonly MeadworksMenuReturn returning = new MeadworksMenuReturn();
        static ConfigEntry<KeyboardShortcut> menuKey;

        /// <summary>True while the chooser window is open.</summary>
        public static bool ChooserOpen => chooser != null && chooser.IsOpen;

        /// <summary>The attached pause-menu button, or null (test and capture tools read it).</summary>
        public static Button Button => button;

        /// <summary>The chooser's panel (its buttons are named <c>Entry_&lt;id&gt;</c>), or null before it was first opened.</summary>
        public static RectTransform ChooserPanel => chooser != null ? chooser.Panel : null;

        /// <summary>The MeadWorks menu key (local to this player; empty means none).</summary>
        public static ConfigEntry<KeyboardShortcut> MenuKey => menuKey;

        internal static void Bind(ConfigFile config)
        {
            menuKey = MeadworksConfig.Bind(config, MeadworksMenuKey.Section, MeadworksMenuKey.Name, new KeyboardShortcut(KeyCode.F8),
                "Opens the Meadworks menu (the window list of Esc > Meadworks) from anywhere in game, except while chat, the console or a text field has the keys. Empty: no key; Esc > Meadworks still works.",
                ConfigScope.Local);
        }

        /// <summary>Plugin Update: the menu key, the chooser's list, and the pause-menu button.</summary>
        internal static void Tick()
        {
            if (!CrucibleUiPlugin.Active || Player.m_localPlayer == null)
            {
                Reset();
                CloseChooser();
                returning.Clear();
                return;
            }
            TickReturn();
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + Recheck;
                listed = Products().Count;
            }
            if (listed == 0)
            {
                Reset();
                CloseChooser();
                PollKey();
                return;
            }
            // Before the pause-menu attachment, which returns early while the game's menu cannot take a button: the key and the
            // chooser do not need the pause menu.
            PollKey();
            TickChooser();
            Menu menu = Menu.instance;
            if (menu == null)
            {
                Reset();
                return;
            }
            if (owner != menu || button == null)
            {
                Reset();
                RectTransform parent = menu.menuEntriesParent;
                if (parent == null || menu.m_settingsButton == null || parent.Find(ButtonName) != null)
                    return;
                Button previous = parent.GetComponentsInChildren<Button>(true)
                    .Where(b => b.transform.parent == parent).OrderBy(b => b.transform.GetSiblingIndex()).LastOrDefault();
                button = CopyMenuButton(menu.m_settingsButton, parent);
                if (button == null)
                    return;
                look = KitNativeMenus.EntryLook.Prepare(button, menu.m_settingsButton);
                owner = menu;
                button.transform.SetAsLastSibling();
                if (parent.GetComponent<LayoutGroup>() == null && previous != null)
                {
                    RectTransform before = (RectTransform)previous.transform;
                    RectTransform rect = (RectTransform)button.transform;
                    rect.anchorMin = before.anchorMin; rect.anchorMax = before.anchorMax;
                    rect.pivot = before.pivot; rect.sizeDelta = before.sizeDelta;
                    rect.anchoredPosition = before.anchoredPosition + Vector2.down * (Mathf.Max(30f, before.rect.height) + 8f);
                }
            }
            RefreshNavigation();
            look?.Apply();
        }

        static void PollKey()
        {
            bool pressed = menuKey != null && MeadworksInput.Pressed(menuKey, MeadworksMenuKey.Context);
            if (!pressed)
                return;
            switch (MeadworksMenuKey.Decide(true, ChooserOpen, Products().Count))
            {
                case MeadworksMenuKeyAction.Open: OpenChooser(); break;
                case MeadworksMenuKeyAction.Close: CloseChooser(); break;
            }
        }

        /// <summary>Opens the chooser (the pause-menu button's click).</summary>
        public static void OpenChooser()
        {
            if (!CrucibleUiPlugin.Active)
                return;
            if (chooser == null || chooser.Panel == null)
            {
                chooser = KitWindow.Create("meadworks_menu", new Vector2(ChooserWidth, 160f));
                if (chooser == null)
                    return;
                chooser.Closed += () => drawn = "";
            }
            drawn = "";
            product = "";
            returning.Clear();
            chooser.Open();
            Draw();
        }

        // Back to the chooser when the window it opened closes.
        static void TickReturn()
        {
            switch (returning.Tick(Time.unscaledTime, KitWindows.AnyOpen))
            {
                case MeadworksMenuReturnAction.CloseGameMenu:
                    if (Menu.IsVisible() && Menu.instance != null)
                    {
                        // Our own close, so the window just opened from the chooser stays (see MenuClosed).
                        closingGameMenu = true;
                        try { Menu.instance.OnClose(); }
                        finally { closingGameMenu = false; }
                    }
                    break;
                case MeadworksMenuReturnAction.Reopen:
                    if (Products().Count > 0)
                        OpenChooser();
                    break;
            }
        }

        public static MeadworksMenuReturn.Suspension SuspendReturn() => returning.Suspend();

        public static void CloseChooser()
        {
            if (chooser != null && chooser.IsOpen)
                chooser.Close();
        }

        static void TickChooser()
        {
            if (chooser == null || !chooser.IsOpen)
                return;
            // The full fingerprint (the product roster, the registry, the language) is built at the list's recheck cadence, not
            // every frame: it allocates a few KB. A changed product or screen size is noticed on the next frame as before.
            bool moved = product != drawnProduct || Screen.width != drawnWidth || Screen.height != drawnHeight;
            if (!moved && drawn.Length > 0 && Time.unscaledTime < nextFingerprint)
                return;
            nextFingerprint = Time.unscaledTime + Recheck;
            string fingerprint = Fingerprint();
            if (fingerprint != drawn)
                Draw();
        }

        static void Draw()
        {
            if (chooser == null || chooser.Panel == null)
                return;
            foreach (Button old in rows)
            {
                if (old == null)
                    continue;
                chooser.Unfocusable(old);
                KitUnity.Retire(old.gameObject);
            }
            rows.Clear();
            Transform title = chooser.Panel.Find("Title");
            if (title != null)
                KitUnity.Retire(title.gameObject);
            Transform empty = chooser.Panel.Find("Empty");
            if (empty != null) KitUnity.Retire(empty.gameObject);
            Transform close = chooser.Panel.Find("Close");
            if (close != null)
            {
                Selectable control = close.GetComponent<Selectable>();
                if (control != null)
                    chooser.Unfocusable(control);
                KitUnity.Retire(close.gameObject);
            }
            var products = Products();
            if (product.Length > 0 && !products.Any(p => p.Id == product)) product = "";
            IReadOnlyList<MeadworksMenuChoice> choices = product.Length == 0 ? products : MeadworksMenuProducts.Actions(product, MeadworksMenu.Registry.Choices());
            if (product.Length > 0 && MeadworksMenuSettings.Available(product))
                choices = choices.Concat(new[] { new MeadworksMenuChoice("@settings", KitText.Text(KitText.MenuSettings, CrucibleUiPlugin.Language)) }).ToArray();
            drawn = Fingerprint();
            drawnProduct = product;
            drawnWidth = Screen.width;
            drawnHeight = Screen.height;
            int count = Mathf.Max(1, MeadworksMenuProducts.Rows(choices.Count));
            float height = 56f + count * (RowHeight + Gap) + RowHeight + Gap + 8f;
            chooser.Panel.sizeDelta = new Vector2(ChooserWidth, height);
            string caption = product.Length == 0 ? MeadworksMenu.Caption : products.First(p => p.Id == product).Caption;
            KitAlmanacDialog.Label(chooser.Panel, "Title", caption, new Vector2(20f, -14f), new Vector2(ChooserWidth - 40f, 32f), 22);
            float y = -50f;
            int index = 0;
            foreach (MeadworksMenuChoice choice in choices)
            {
                string id = choice.Id;
                var cell = MeadworksMenuProducts.Cell(index);
                float width = (ChooserWidth - 40f - Gap) / 2f;
                Button row = KitButton.Create(chooser.Panel, "Entry_" + id, choice.Caption, () => Select(id),
                    new Vector2(20f + cell.X * (width + Gap), y - cell.Y * (RowHeight + Gap)), new Vector2(width, RowHeight));
                if (row != null)
                {
                    // What the product does, in one sentence, on hover.
                    string tip = product.Length > 0 ? MeadworksMenuTips.ActionText(id, CrucibleUiPlugin.Language)
                        : MeadworksMenuTips.Text(id, CrucibleUiPlugin.Language);
                    if (tip.Length > 0)
                    {
                        KitTooltip.SetMenu(row.gameObject, choice.Caption, tip);
                    }
                    rows.Add(row);
                    chooser.Focusable(row, cell.X, cell.Y);
                }
                index++;
            }
            y -= count * (RowHeight + Gap);
            if (choices.Count == 0)
                KitAlmanacDialog.Label(chooser.Panel, "Empty", KitText.Text(KitText.BlockedTopic, CrucibleUiPlugin.Language), new Vector2(20f, -56f), new Vector2(ChooserWidth - 40f, RowHeight), 18);
            Button shut = KitButton.Create(chooser.Panel, "Close", KitText.Text(product.Length > 0 ? KitText.MenuBack : KitText.Close, CrucibleUiPlugin.Language),
                () => { if (product.Length > 0) { product = ""; Draw(); } else chooser.Close(); }, new Vector2(20f, y - 4f), new Vector2(ChooserWidth - 40f, RowHeight));
            if (shut != null)
            {
                KitTooltip.Set(shut.gameObject, KitText.Text(product.Length > 0 ? KitText.MenuBack : KitText.Close, CrucibleUiPlugin.Language),
                    MeadworksMenuTips.ActionText(product.Length > 0 ? "@back" : "@close", CrucibleUiPlugin.Language));
                chooser.Focusable(shut, 0, count);
                chooser.Focusable(shut, 1, count);
            }
        }

        public static IReadOnlyList<MeadworksMenuChoice> LoadedProducts() => MeadworksMenuProducts.Roster(Chainloader.PluginInfos.Values
            .Where(p => p.Instance != null).Select(p => new MeadworksMenuChoice(p.Metadata.GUID, p.Metadata.Name)));
        static IReadOnlyList<MeadworksMenuChoice> Products() => LoadedProducts();

        static string Fingerprint() => product + "|" + CrucibleUiPlugin.Language + "|" + string.Join("|", Products().Select(p => p.Id + "=" + p.Caption))
            + "|" + MeadworksMenu.Registry.Fingerprint() + "|" + MeadworksMenuSettings.Available(product) + "|" + Screen.width + "x" + Screen.height;

        static void Select(string id)
        {
            if (product.Length == 0)
            {
                CloseChooser();
                returning.Chosen(Time.unscaledTime);
                var opened = MeadworksMenuProducts.Open(id, MeadworksMenu.Registry);
                if (opened == MeadworksProductOpen.Settings || opened == MeadworksProductOpen.Feature) return;
                returning.Clear();
                product = opened == MeadworksProductOpen.Choices ? id : "";
                chooser.Open(); Draw(); return;
            }
            if (id != "@settings") { Choose(id); return; }
            CloseChooser();returning.Chosen(Time.unscaledTime);
            if (!MeadworksMenuSettings.Open(product)) returning.Clear();
        }

        static void Choose(string id)
        {
            CloseChooser();
            returning.Chosen(Time.unscaledTime);
            if (!MeadworksMenu.Registry.Open(id))
                returning.Clear();
        }

        static Button CopyMenuButton(Button source, RectTransform parent)
        {
            GameObject go = UnityEngine.Object.Instantiate(source.gameObject, parent);
            go.name = ButtonName;
            // Keep the native caption geometry and hover writers. A tab caption's truncation,
            // margins and palette are inappropriate for the game's display-font menu.
            foreach (UIGamePad pad in go.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null && pad.m_hint.transform.IsChildOf(go.transform))
                    UnityEngine.Object.DestroyImmediate(pad.m_hint);
                UnityEngine.Object.DestroyImmediate(pad);
            }
            foreach (Localize localize in go.GetComponentsInChildren<Localize>(true))
                UnityEngine.Object.DestroyImmediate(localize);
            // The native prefab may have more than one text layer (including hidden states).
            // Update every layer so a selected button cannot expose the old Settings word.
            foreach (TMPro.TMP_Text text in go.GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.text = MeadworksMenu.Caption;
            Button copy = go.GetComponent<Button>();
            copy.onClick = new Button.ButtonClickedEvent();
            copy.onClick.AddListener(OpenChooser);
            copy.navigation = new Navigation { mode = Navigation.Mode.None };
            go.SetActive(true);
            return copy;
        }

        static void RefreshNavigation()
        {
            if (owner == null || button == null)
                return;
            Button anchor = owner.m_quitButton;
            if (anchor == null || !anchor.gameObject.activeSelf)
                anchor = owner.m_logoutButton;
            if (anchor == null)
                return;
            HashSet<Selectable> visited = navigationVisited;
            visited.Clear();
            Selectable cursor = anchor.navigation.selectOnDown;
            for (int i = 0; cursor != null && i < 256; i++)
            {
                if (cursor == button)
                    return;
                if (cursor == anchor || !visited.Add(cursor))
                    break;
                cursor = cursor.navigation.selectOnDown;
            }
            Navigation before = anchor.navigation;
            links[anchor] = before;
            Navigation after = before;
            after.mode = Navigation.Mode.Explicit;
            after.selectOnDown = button;
            anchor.navigation = after;
            Selectable next = before.selectOnDown;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = anchor, selectOnDown = next };
            if (next != null && next.navigation.selectOnUp == anchor)
            {
                links[next] = next.navigation;
                Navigation navigation = next.navigation;
                navigation.selectOnUp = button;
                next.navigation = navigation;
            }
        }

        /// <summary>Removes the button and every navigation link to it.</summary>
        internal static void Reset()
        {
            if (button != null)
            {
                Navigation own = button.navigation;
                if (owner != null && owner.menuEntriesParent != null)
                {
                    foreach (Selectable control in owner.menuEntriesParent.GetComponentsInChildren<Selectable>(true))
                    {
                        if (control == null || control == button)
                            continue;
                        Navigation current = control.navigation;
                        if (current.selectOnDown == button) current.selectOnDown = own.selectOnDown != button ? own.selectOnDown : null;
                        if (current.selectOnUp == button) current.selectOnUp = own.selectOnUp != button ? own.selectOnUp : null;
                        control.navigation = current;
                    }
                }
                foreach (KeyValuePair<Selectable, Navigation> link in links)
                {
                    if (link.Key == null)
                        continue;
                    Navigation current = link.Key.navigation;
                    if (current.selectOnDown == button) current.selectOnDown = link.Value.selectOnDown;
                    if (current.selectOnUp == button) current.selectOnUp = link.Value.selectOnUp;
                    link.Key.navigation = current;
                }
                look?.Dispose();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                KitUnity.Retire(button.gameObject);
            }
            links.Clear();
            look = null;
            button = null;
            owner = null;
        }

        internal static void Shutdown()
        {
            Reset();
            if (chooser != null)
            {
                chooser.Destroy();
                chooser = null;
            }
            rows.Clear();
            returning.Clear();
        }

        /// <summary>
        /// The pause menu closing (Esc, Continue, the pad's B) closes the MeadWorks chooser left open over it, so it does not
        /// stay on screen once the game runs again (user report, October 20, 2026). Not when the chooser itself hid the
        /// menu to show a window it opened, and not when the menu was already hidden (the chooser opened with the menu key).
        /// </summary>
        [HarmonyPatch(typeof(Menu), nameof(Menu.Hide))]
        static class MenuClosed
        {
            static void Prefix(Menu __instance, out bool __state) => __state = Shown(__instance);

            static void Postfix(Menu __instance, bool __state)
            {
                if (!__state || closingGameMenu || Shown(__instance) || !ChooserOpen)
                    return;
                returning.Clear();
                CloseChooser();
            }

            static bool Shown(Menu menu) => menu != null && menu.m_root != null && menu.m_root.gameObject.activeSelf;
        }

        [HarmonyPatch(typeof(Menu), "UpdateNavigation")]
        static class NavigationRefresh
        {
            [HarmonyPostfix, HarmonyPriority(Priority.Last)]
            static void Postfix(Menu __instance)
            {
                if (__instance == owner)
                    RefreshNavigation();
            }
        }
    }
}

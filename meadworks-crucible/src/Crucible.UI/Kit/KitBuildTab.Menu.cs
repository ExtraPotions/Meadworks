// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// KX06 tab button in the build menu; behaviour as OpenKeep 3.0.0 BlueprintTab (see KitBuildTab.cs), rebuilt generic.
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    public sealed partial class KitBuildTab
    {
        /// <summary>The build menu that woke last (one per Hud), or null.</summary>
        private static BuildUi menu;

        private Button button;
        private KitBuildPieceList pieceList;
        private int index = -1;
        private Color imageColor;
        private Color textColor;
        private bool blockedLook;

        /// <summary>The tab is in the build menu that exists now.</summary>
        public bool Installed => menu != null && button != null && index >= 0;

        /// <summary>Why the current menu got no tab (a <see cref="KitBuildTabRules.MenuRefusal"/> code), or null.</summary>
        public string Refusal { get; private set; }

        /// <summary>The open build menu shows this tab.</summary>
        public bool Showing => Installed && menu.gameObject.activeSelf && KitBuildMenuAccess.CurrentList(menu) == index;

        /// <summary>BuildUi.Awake postfix: every registered tab joins the new menu.</summary>
        internal static void MenuAwake(BuildUi ui)
        {
            menu = ui;
            foreach (KitBuildTab tab in All)
            {
                try
                {
                    tab.Install(ui);
                }
                catch (System.Exception e)
                {
                    CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {tab.Owner} install failed: {e}");
                }
            }
        }

        /// <summary>BuildUi.OnDestroy postfix: the menu's tabs went with it.</summary>
        internal static void MenuDestroyed(BuildUi ui)
        {
            if (ui != menu)
                return;
            menu = null;
            foreach (KitBuildTab tab in All)
                tab.Forget();
        }

        private void Forget()
        {
            button = null;
            pieceList = null;
            index = -1;
            blockedLook = false;
        }

        private void Install(BuildUi ui)
        {
            Forget();
            List<IPieceList> lists = KitBuildMenuAccess.PieceLists(ui);
            List<Button> buttons = KitBuildMenuAccess.TabButtons(ui);
            TabHandler handler = KitBuildMenuAccess.Handler(ui);
            RectTransform row = KitBuildMenuAccess.TabContainer(ui);
            Refusal = !KitBuildMenuAccess.Complete ? "menu_members_missing"
                : KitBuildTabRules.MenuRefusal(lists?.Count ?? 0, buttons?.Count ?? -1, handler != null, handler?.m_tabs?.Count ?? -1, row != null ? row.childCount : 0);
            int template = -1;
            if (Refusal == null)
            {
                var owned = new bool[lists.Count];
                for (int i = 0; i < lists.Count; i++)
                    owned[i] = lists[i] != null && lists[i].GetType().Assembly == typeof(BuildUi).Assembly && buttons[i] != null;
                template = KitBuildTabRules.TemplateIndex(owned);
                if (template < 0)
                    Refusal = "no_template";
            }
            if (Refusal != null)
            {
                CrucibleUiPlugin.Log?.Warning($"Crucible.UI: the build menu is not as expected ({Refusal}); no {Caption} tab for {Owner}.");
                return;
            }
            index = lists.Count;
            pieceList = new KitBuildPieceList(this);
            lists.Add(pieceList);
            button = MakeButton(row, buttons[template]);
            buttons.Add(button);
            handler.m_tabs.Add(new TabHandler.Tab { m_button = null, m_onClick = new UnityEvent() });
            State = KitBuildTabState.Hidden;
            button.gameObject.SetActive(false);
        }

        private Button MakeButton(RectTransform row, Button template)
        {
            GameObject copy = Object.Instantiate(template.gameObject, row, false);
            copy.name = "Meadworks_BuildTab_" + Owner;
            copy.transform.SetAsLastSibling();
            foreach (Localize localizer in copy.GetComponentsInChildren<Localize>(true))
                Object.Destroy(localizer);
            Button made = copy.GetComponent<Button>();
            made.onClick = new Button.ButtonClickedEvent();
            made.onClick.AddListener(Clicked);
            foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
                text.text = Localization.instance != null ? Localization.instance.Localize(Caption) : Caption;
            Image image = copy.GetComponent<Image>();
            imageColor = image != null ? image.color : Color.white;
            TMP_Text label = copy.GetComponentInChildren<TMP_Text>(true);
            textColor = label != null ? label.color : Color.white;
            return made;
        }

        private void Clicked()
        {
            try
            {
                if (!disposed && Installed && State == KitBuildTabState.Shown && KitBuildMenuAccess.BuildTool(menu) != null)
                    menu.SelectPieceList(index);
            }
            catch (System.Exception e)
            {
                CrucibleUiPlugin.Log?.Error($"Crucible.UI: build tab {Owner} click failed: {e}");
            }
        }

        /// <summary>Applies the product's view to the button, the handler and the menu's current tab.</summary>
        private void TickView()
        {
            if (disposed || !Installed)
                return;
            List<IPieceList> lists = KitBuildMenuAccess.PieceLists(menu);
            TabHandler handler = KitBuildMenuAccess.Handler(menu);
            if (lists == null || handler == null || index >= lists.Count || index >= handler.m_tabs.Count || lists[index] != pieceList)
            {
                // Another mod reshaped the menu after the install: the tab lets go once instead of failing every frame.
                Refusal = "menu_changed";
                CrucibleUiPlugin.Log?.Warning($"Crucible.UI: the build menu changed under the {Caption} tab of {Owner}; the tab is left out until the menu wakes again.");
                if (button != null)
                    button.gameObject.SetActive(false);
                Forget();
                return;
            }
            Player player = Player.m_localPlayer;
            PieceTable tool = player != null ? player.GetBuildTool() : KitBuildMenuAccess.BuildTool(menu);
            KitBuildTabView now = Decide(tool);
            int current = KitBuildMenuAccess.CurrentList(menu);
            bool open = menu.gameObject.activeSelf;
            KitBuildTabPlan plan = KitBuildTabRules.Plan(now, current, index, open, KitBuildMenuAccess.BuildTool(menu) != null);
            bool wasActive = button.gameObject.activeSelf;
            bool wasReached = handler.m_tabs[index].m_button != null;
            State = now.State;
            if (wasActive != plan.ButtonActive)
                button.gameObject.SetActive(plan.ButtonActive);
            if (wasReached != plan.KeysReach)
            {
                handler.m_tabs[index].m_button = plan.KeysReach ? button : null;
                button.interactable = plan.Interactable;
            }
            else if (!plan.KeysReach)
            {
                button.interactable = false;
            }
            Look(now);
            if (plan.ButtonActive && !wasActive)
                Place();
            if (plan.Leave == KitBuildTabLeave.SelectFirst)
                menu.SelectPieceList(0);
            else if (plan.Leave == KitBuildTabLeave.ResetIndex)
                KitBuildMenuAccess.SetCurrentList(menu, 0);
        }

        /// <summary>Blocked: dimmed with the reason as tooltip (C03); otherwise the row's own look.</summary>
        private void Look(KitBuildTabView now)
        {
            bool blocked = now.State == KitBuildTabState.Blocked;
            if (blocked)
            {
                KitButton.Block(button, now.Blocked);
                blockedLook = true;
                return;
            }
            if (!blockedLook)
                return;
            blockedLook = false;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = imageColor;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.color = textColor;
            if (button.GetComponent<UITooltip>() is UITooltip tip)
                tip.Set("", "");
        }

        /// <summary>A row without a layout group: the button goes after the last visible tab at the row's own spacing.</summary>
        private void Place()
        {
            Transform row = button.transform.parent;
            button.transform.SetAsLastSibling();
            if (row == null || row.GetComponent<LayoutGroup>() != null)
                return;
            var tabs = new List<RectTransform>();
            foreach (Transform child in row)
                if (child != button.transform && child.gameObject.activeSelf && child.GetComponent<Button>() != null && child is RectTransform rect)
                    tabs.Add(rect);
            if (tabs.Count == 0)
                return;
            Vector2 step = tabs.Count > 1 ? tabs[1].anchoredPosition - tabs[0].anchoredPosition : new Vector2(tabs[0].rect.width, 0);
            if (step.sqrMagnitude < 1)
                step = new Vector2(tabs[tabs.Count - 1].rect.width, 0);
            ((RectTransform)button.transform).anchoredPosition = tabs[tabs.Count - 1].anchoredPosition + step;
        }

        /// <summary>The menu shows this tab: its buttons are drawn again (refill), and the hover info follows the button under the cursor.</summary>
        private void Redraw()
        {
            if (!Showing || KitBuildMenuAccess.BuildTool(menu) == null)
                return;
            KitBuildMenuAccess.UpdatePieceButtons(menu);
            Piece hovered = KitBuildMenuAccess.Hovered(menu)?.Piece;
            if (hovered != null && Hud.instance != null)
                Hud.instance.OnHoverPiece(hovered);
        }

        /// <summary>Teardown: the last tab leaves every list; a tab with later tabs stays as an empty hidden place.</summary>
        private void Uninstall()
        {
            if (!Installed)
                return;
            List<IPieceList> lists = KitBuildMenuAccess.PieceLists(menu);
            List<Button> buttons = KitBuildMenuAccess.TabButtons(menu);
            TabHandler handler = KitBuildMenuAccess.Handler(menu);
            if (KitBuildMenuAccess.CurrentList(menu) == index)
            {
                if (menu.gameObject.activeSelf && KitBuildMenuAccess.BuildTool(menu) != null)
                    menu.SelectPieceList(0);
                else
                    KitBuildMenuAccess.SetCurrentList(menu, 0);
            }
            pieceList.Dead = true;
            bool aligned = lists.Count == buttons.Count && handler.m_tabs.Count == lists.Count && index < lists.Count && lists[index] == pieceList;
            if (aligned && KitBuildTabRules.Teardown(index, lists.Count) == KitBuildTabTeardown.Remove)
            {
                lists.RemoveAt(index);
                buttons.RemoveAt(index);
                handler.m_tabs.RemoveAt(index);
                Object.Destroy(button.gameObject);
            }
            else if (index < handler.m_tabs.Count)
            {
                handler.m_tabs[index].m_button = null;
                button.onClick.RemoveAllListeners();
                button.gameObject.SetActive(false);
            }
            Forget();
        }
    }

    /// <summary>A tab's piece list: its shown entries in order, for its own piece table only; the game's tag column on request.</summary>
    internal sealed class KitBuildPieceList : IPieceList
    {
        private readonly KitBuildTab tab;

        /// <summary>The tab was torn down while later tabs keep their places: the list stays empty.</summary>
        public bool Dead;

        public KitBuildPieceList(KitBuildTab tab)
        {
            this.tab = tab;
        }

        public string DisplayName => tab.Caption;

        public bool ShowTags => !Dead && tab.TagColumn;

        public bool CanCustomizeTags => false;

        public int TagCount => 0;

        public int TagSeparatorIndex => -1;

        public string GetTagDisplayName(int index) => "";

        public int GetTagIdByIndex(int index) => -1;

        public void UpdateAvailableTags(PieceTable pieceTable)
        {
        }

        public void GetAvailablePiecesWithTag(int tagId, PieceTable pieceTable, IList<Piece> resultOut)
        {
            if (!Dead)
                tab.List(pieceTable, resultOut);
        }
    }
}

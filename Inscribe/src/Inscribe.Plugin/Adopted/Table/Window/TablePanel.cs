// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Text;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The Rune Table's window on one inventory screen (rune-table.md section 6): made from the game's crafting panel the
    /// first time it opens (<see cref="PanelBuilder"/>, <see cref="PanelLayout"/>), shown inside the crafting panel over
    /// its covered parts (so it slides in and out with it), and filled by the chosen tab. Local player only.
    /// </summary>
    internal sealed class TablePanel
    {
        private readonly InventoryGui _gui;
        // Sacrifice always last, the furthest right (user 2026-10-07); a hidden tab leaves no gap (PanelLayout.PlaceTabs).
        private readonly TableTab[] _tabs = { new InscribeTab(), new SocketTab(), new SacrificeTab() };
        private int _tab;
        private readonly CraftingCover _cover = new CraftingCover();

        public TablePanel(InventoryGui gui)
        {
            _gui = gui;
            Parts = PanelParts.From(PanelBuilder.Build(gui.m_crafting));
            PanelLayout.Arrange(Parts, gui, PickTab, i => Current.PickRune(i), i => Current.PickEssence(i));
            Parts.Action?.onClick.AddListener(() => TableWindow.Run(view => Current.Act(view, TableWindow.ShiftHeld)));
            Parts.Extra?.onClick.AddListener(() => TableWindow.Run(view => Current.Extra(view)));
        }

        public PanelParts Parts { get; }

        /// <summary>False once the inventory screen it was made on is gone (a logout).</summary>
        public bool Alive => _gui != null && Parts.Root != null;

        private TableTab Current => _tabs[_tab];

        /// <summary>
        /// Shows the window inside the crafting panel, filling it, the panel's own parts covered (<see cref="CraftingCover"/>):
        /// the inventory's animations slide the panel, so the window opens and leaves with the other panels, and a UI mod's
        /// size or place for the panel is followed by itself.
        /// </summary>
        public void Show()
        {
            RectTransform rect = Parts.Rect;
            if (rect.parent != _gui.m_crafting)
            {
                rect.SetParent(_gui.m_crafting, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            rect.SetAsLastSibling();
            Parts.Root.SetActive(true);
            _cover.Cover(_gui.m_crafting, rect);
        }

        /// <summary>Keeps the window the panel's last child (drawn over anything another mod added since).</summary>
        public void Follow()
        {
            if (Parts.Rect.GetSiblingIndex() != Parts.Rect.parent.childCount - 1)
            {
                Parts.Rect.SetAsLastSibling();
            }
        }

        public void Hide()
        {
            _cover.Uncover();
            if (Parts.Root != null)
            {
                Parts.Root.SetActive(false);
            }
        }

        public void Render(TableView view)
        {
            if (Parts.Title != null)
            {
                Parts.Title.text = Words.Localize(TableWords.Name);
            }
            _tab = _tabs[_tab].Shown ? _tab : 0;
            for (int i = 0; i < Parts.Tabs.Length; i++)
            {
                Parts.Tabs[i].gameObject.SetActive(_tabs[i].Shown);
                Parts.Tabs[i].interactable = i != _tab;
                TMPro.TMP_Text? label = Parts.LabelOf(Parts.Tabs[i]);
                if (label != null)
                {
                    label.text = Words.Localize(_tabs[i].Label);
                }
            }
            PanelLayout.PlaceTabs(Parts, i => _tabs[i].Shown);
            Parts.Recasting?.Show(false);
            Current.Fill(view);
        }

        private void PickTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _tabs.Length - 1);
            TableWindow.MarkDirty();
        }
    }
}

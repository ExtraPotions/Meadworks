// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The parts of the Rune Table's window the tabs fill (rune-table.md section 6), found by name in the copy of the
    /// crafting panel (<see cref="PanelBuilder"/>). A part a UI mod removed stays null and is skipped.
    /// </summary>
    internal sealed class PanelParts
    {
        private PanelParts(GameObject root)
        {
            Root = root;
            Rect = (RectTransform)root.transform;
            Title = Find<TMP_Text>("topic");
            TabTemplate = Find<Button>("TabsButtons/Craft");
            Scroll = Find<ScrollRect>("RecipeList/Recipes");
            Description = Find<RectTransform>("Decription");
            Icon = Find<Image>("Decription/Icon");
            Name = Find<TMP_Text>("Decription/Name");
            Text = Find<TMP_Text>("Decription/Description");
            Requirements = Find<RectTransform>("Decription/requirements");
            Action = Find<Button>("Decription/craft_button_panel/CraftButton");
            Extra = Find<Button>("Decription/SelectVariant");
        }

        public GameObject Root { get; }
        public RectTransform Rect { get; }
        public TMP_Text? Title { get; }
        public Button? TabTemplate { get; }
        public ScrollRect? Scroll { get; }
        public RectTransform? Description { get; }
        public Image? Icon { get; }
        public TMP_Text? Name { get; }
        public TMP_Text? Text { get; }
        public RectTransform? Requirements { get; }
        public Button? Action { get; }
        public Button? Extra { get; }

        /// <summary>The window's tabs, made from the copied tab (<see cref="PanelLayout"/>).</summary>
        public Button[] Tabs { get; set; } = new Button[0];

        /// <summary>The first tab's place and the distance between tabs (<see cref="PanelLayout.PlaceTabs"/>).</summary>
        public Vector2 TabOrigin { get; set; }

        public float TabStep { get; set; }

        public ListRows? List { get; set; }
        public SlotRow? Runes { get; set; }
        public SlotRow? Essences { get; set; }
        public TMP_Text? RuneLabel { get; set; }
        public TMP_Text? EssenceLabel { get; set; }
        public CostRow? Cost { get; set; }

        /// <summary>The description text's whole area (from the description's top left); the pills take its bottom.</summary>
        public Rect TextArea { get; set; }

        public PillStrip? Pills { get; set; }

        public static PanelParts From(GameObject root) => new PanelParts(root);

        public TMP_Text? LabelOf(Button? button) => button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;

        private T? Find<T>(string path) where T : Component
        {
            Transform? part = Root.transform.Find(path);
            return part != null ? part.GetComponent<T>() : null;
        }
    }
}

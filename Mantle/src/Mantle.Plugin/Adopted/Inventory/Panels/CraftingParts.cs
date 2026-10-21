using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// The crafting panel's parts that <see cref="CraftingPanel"/> moves or resizes, found through the game's own
    /// references (checked against the scene: <c>Crafting/RecipeList/Recipes/ListRoot</c> is <c>m_recipeListRoot</c>,
    /// <c>Crafting/Decription</c> holds <c>m_recipeName</c> and <c>m_recipeDecription</c>, <c>Crafting/TabsButtons</c>
    /// holds the tabs and <c>TabBorder</c>; <c>root/Info</c>, the name, skills, trophies and PvP panel above it, is
    /// <c>m_infoPanel</c>) and remembered as the game made them. The recipe row template's name
    /// widens with the list, and its quality number and durability bar, anchored to the row's centre, move back by
    /// half so they stay over the icon. Null when the panel is not the game's shape (another mod rebuilt it).
    /// </summary>
    public sealed class CraftingParts
    {
        public RectMemo Panel { get; private set; }
        public RectMemo List { get; private set; }
        public RectMemo View { get; private set; }
        public RectMemo Description { get; private set; }
        public RectMemo Name { get; private set; }
        public RectMemo Text { get; private set; }
        public RectMemo Tabs { get; private set; }
        public RectMemo TabLine { get; private set; }
        public RectMemo Info { get; private set; }
        public float BaseSize { get; private set; }

        private RectMemo row;
        private RectMemo rowName;
        private RectMemo rowLevel;
        private RectMemo rowBar;

        public bool Alive => Panel.Alive && List.Alive && Description.Alive;

        public static CraftingParts Find(InventoryGui gui)
        {
            Transform view = gui.m_recipeListRoot != null ? gui.m_recipeListRoot.parent : null;
            Transform list = view != null ? view.parent : null;
            Transform description = gui.m_recipeDecription != null ? gui.m_recipeDecription.transform.parent : null;
            if (list == null || list.parent != gui.m_crafting || description == null || description.parent != gui.m_crafting)
                return null;
            CraftingParts parts = new CraftingParts
            {
                Panel = RectMemo.Of(gui.m_crafting), List = RectMemo.Of(list), View = RectMemo.Of(view),
                Description = RectMemo.Of(description), BaseSize = gui.m_recipeListBaseSize,
                Info = gui.m_infoPanel != null && gui.m_infoPanel.parent == gui.m_crafting.parent ? RectMemo.Of(gui.m_infoPanel) : null,
            };
            parts.FindHeader(gui);
            parts.FindRow(gui.m_recipeElementPrefab);
            return parts.List != null && parts.View != null && parts.Description != null ? parts : null;
        }

        private void FindHeader(InventoryGui gui)
        {
            Name = gui.m_recipeName != null ? RectMemo.Of(gui.m_recipeName.transform) : null;
            Text = RectMemo.Of(gui.m_recipeDecription.transform);
            Transform tabs = gui.m_tabCraft != null ? gui.m_tabCraft.transform.parent : null;
            if (tabs == null || tabs.parent != gui.m_crafting)
                return;
            Tabs = RectMemo.Of(tabs);
            TabLine = RectMemo.Of(tabs.Find("TabBorder"));
        }

        private void FindRow(GameObject template)
        {
            if (template == null)
                return;
            row = RectMemo.Of(template.transform);
            rowName = RectMemo.Of(template.transform.Find("name"));
            rowLevel = RectMemo.Of(template.transform.Find("QualityLevel"));
            rowBar = RectMemo.Of(template.transform.Find("Durability"));
        }

        /// <summary>The template the game copies for every recipe row, that much wider.</summary>
        public void LayRow(float wider)
        {
            row?.Set(Vector2.zero, new Vector2(wider, 0f));
            rowName?.Set(Vector2.zero, new Vector2(wider, 0f));
            rowLevel?.Set(new Vector2(-wider / 2f, 0f), Vector2.zero);
            rowBar?.Set(new Vector2(-wider / 2f, 0f), Vector2.zero);
        }
    }
}

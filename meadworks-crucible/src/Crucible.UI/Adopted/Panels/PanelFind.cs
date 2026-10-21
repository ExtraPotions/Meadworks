// Adopted from PackPanel 0.12.0 (ValheimMods, https://github.com/geraldjglasgow/ValheimMods)
// Upstream: PackPanel/PackPanel/src/Panels/PanelFind.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: looks under any parent panel instead of only the
// inventory's player panel; the InventoryGui overloads keep the upstream behaviour.
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// One of a product's own panels under a parent, found by name once per parent and kept while it lives: pop-ups are
    /// asked for every frame a screen is open. That none is there is remembered too, until the panel is made
    /// (<see cref="Made(Transform, RectTransform)"/>) or the parent is a new one.
    /// </summary>
    public sealed class PanelFind
    {
        private readonly string name;
        private Transform foundIn;
        private RectTransform found;

        public PanelFind(string name) => this.name = name;

        public RectTransform In(InventoryGui gui) => gui != null ? In(gui.m_player) : null;

        public RectTransform In(Transform parent)
        {
            if (parent == null)
                return null;
            if (ReferenceEquals(parent, foundIn) && (ReferenceEquals(found, null) || found != null))
                return found;
            foundIn = parent;
            found = parent.Find(name) as RectTransform;
            return found;
        }

        public void Made(InventoryGui gui, RectTransform panel) => Made(gui != null ? gui.m_player : null, panel);

        public void Made(Transform parent, RectTransform panel)
        {
            foundIn = parent;
            found = panel;
        }
    }
}

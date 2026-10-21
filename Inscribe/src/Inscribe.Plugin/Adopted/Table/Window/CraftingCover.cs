// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The crafting panel's own parts made invisible while the Rune Table's window sits inside it (user 2026-10-07: the
    /// window "just stays and disappears" instead of sliding off with the other panels): each direct child but the window
    /// gets a CanvasGroup at alpha 0 that takes no clicks, so the panel itself stays active and the inventory's show and
    /// hide animations move it, the window with it. The game keeps updating the hidden parts, unseen. A child that had a
    /// CanvasGroup of its own gets its values back; one added here is removed.
    /// </summary>
    internal sealed class CraftingCover
    {
        private readonly struct Hidden
        {
            public Hidden(CanvasGroup group, bool added, float alpha, bool blocks, bool interactable)
            {
                Group = group;
                Added = added;
                Alpha = alpha;
                Blocks = blocks;
                Interactable = interactable;
            }

            public CanvasGroup Group { get; }
            public bool Added { get; }
            public float Alpha { get; }
            public bool Blocks { get; }
            public bool Interactable { get; }
        }

        private readonly List<Hidden> _hidden = new List<Hidden>();

        public void Cover(Transform crafting, Transform keep)
        {
            Uncover();
            foreach (Transform child in crafting)
            {
                if (child == keep)
                {
                    continue;
                }
                CanvasGroup? own = child.GetComponent<CanvasGroup>();
                bool added = own == null;
                CanvasGroup group = own != null ? own : child.gameObject.AddComponent<CanvasGroup>();
                _hidden.Add(new Hidden(group, added, group.alpha, group.blocksRaycasts, group.interactable));
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        public void Uncover()
        {
            foreach (Hidden hidden in _hidden)
            {
                if (hidden.Group == null)
                {
                    continue;
                }
                if (hidden.Added)
                {
                    Object.Destroy(hidden.Group);
                    continue;
                }
                hidden.Group.alpha = hidden.Alpha;
                hidden.Group.blocksRaycasts = hidden.Blocks;
                hidden.Group.interactable = hidden.Interactable;
            }
            _hidden.Clear();
        }
    }
}

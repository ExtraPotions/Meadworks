// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Items;
using UnityEngine;
using UnityEngine.UI;
using ExtraPotions.Meadworks.Crucible.UI;

namespace ExtraPotions.Inscribe.Adopted.Display.Backdrops
{
    /// <summary>
    /// The sockets of an item on its icon (user request 2026-10-07: "a little circle representing a socket ... if gem is in
    /// it, the gems color should show"): a small round mark per socket in the icon's lower right corner, inside the rarity
    /// backdrop's inner line: the first in the corner, the second above it, the third left of the first. An empty socket
    /// is a dark hollow (<see cref="SocketArt.Ring"/>); a filled one shows its gem (<see cref="SocketArt.Gem"/>) in the gem's
    /// colour. Made once per icon as the sibling right after it, following the icon's rect, shown only while the icon is
    /// and the item has sockets; repainted only when the item's state changes. Driven by <see cref="IconBackdrop"/> on every
    /// surface (Normal items too: the chisel sockets them). Display only, on the viewing client.
    /// </summary>
    internal static class IconSockets
    {
        private const string Name = "ecf_sockets";
        private const float Units = 64f;
        private const float Pip = 9f;
        private const float Inset = 9f;
        private const float Gap = 1f;

        // Each socket's lower left corner in the icon's 64 units.
        private static readonly Vector2[] Corners =
        {
            new Vector2(Units - Inset - Pip, Inset),
            new Vector2(Units - Inset - Pip, Inset + Pip + Gap),
            new Vector2(Units - Inset - 2f * Pip - Gap, Inset),
        };

        private sealed class Made
        {
            public RectTransform Holder = null!;
            public Image[] Rings = new Image[Corners.Length];
            public Image[] Gems = new Image[Corners.Length];
            public ItemState? Shown;
        }

        private static readonly Dictionary<Image, Made> All = new Dictionary<Image, Made>();
        private static readonly List<Image> Gone = new List<Image>();
        private static int _sweepAt = 64;
        private static int _shown;

        public static void Set(Image icon, ItemDrop.ItemData? item)
        {
            ItemState? state = Socketed(item);
            if (state == null)
            {
                Hide(icon);
                return;
            }
            Made made = Get(icon);
            Fit(made.Holder, icon.rectTransform);
            if (!made.Holder.gameObject.activeSelf)
            {
                made.Holder.gameObject.SetActive(true);
                _shown++;
            }
            if (made.Shown != state)
            {
                Paint(made, state);
                made.Shown = state;
            }
        }

        public static void Hide(Image? icon)
        {
            if (_shown == 0 || icon == null || !All.TryGetValue(icon, out Made made) || made.Holder == null
                || !made.Holder.gameObject.activeSelf)
            {
                return;
            }
            made.Holder.gameObject.SetActive(false);
            _shown--;
        }

        internal static void HideAll()
        {
            foreach (Made made in All.Values)
                if (made.Holder != null) made.Holder.gameObject.SetActive(false);
            _shown = 0;
        }

        internal static void Shutdown()
        {
            foreach (Made made in All.Values)
                if (made.Holder != null) KitUnity.Retire(made.Holder.gameObject);
            All.Clear();
            Gone.Clear();
            _shown = 0;
            _sweepAt = 64;
        }

        internal static void Fade(Image icon, float seconds, float alpha)
        {
            if (All.TryGetValue(icon, out Made made) && made.Holder != null)
                foreach (Graphic graphic in made.Holder.GetComponentsInChildren<Graphic>(true)) IconBackdrop.FadeGraphic(graphic, seconds, alpha);
        }

        private static ItemState? Socketed(ItemDrop.ItemData? item)
        {
            if (item == null || item.m_customData == null || item.m_customData.Count == 0)
            {
                return null;
            }
            ItemState state = ItemState.Read(item);
            return state.Sockets > 0 ? state : null;
        }

        private static void Paint(Made made, ItemState state)
        {
            for (int i = 0; i < Corners.Length; i++)
            {
                made.Rings[i].enabled = i < state.Sockets;
                made.Gems[i].enabled = false;
            }
            for (int g = 0; g < state.Gems.Count; g++)
            {
                int socket = state.GemSocketAt(g);
                if (socket >= 0 && socket < Corners.Length && socket < state.Sockets)
                {
                    made.Gems[socket].color = StoneVisuals.Tint(state.Gems[g].GemId);
                    made.Gems[socket].enabled = true;
                }
            }
        }

        private static Made Get(Image icon)
        {
            if (All.TryGetValue(icon, out Made made) && made.Holder != null)
            {
                return made;
            }
            Forget();
            made = Make(icon);
            All[icon] = made;
            return made;
        }

        // Icons the game destroyed (recipe lists rebuilt, drag ghosts) are dropped once the table has doubled.
        private static void Forget()
        {
            if (All.Count < _sweepAt)
            {
                return;
            }
            Gone.Clear();
            _shown = 0;
            foreach (KeyValuePair<Image, Made> pair in All)
            {
                if (pair.Key == null || pair.Value.Holder == null)
                {
                    Gone.Add(pair.Key!);
                }
                else if (pair.Value.Holder.gameObject.activeSelf)
                {
                    _shown++;
                }
            }
            Gone.ForEach(icon => All.Remove(icon));
            _sweepAt = System.Math.Max(64, All.Count * 2);
        }

        private static Made Make(Image icon)
        {
            var holder = new GameObject(Name, typeof(RectTransform), typeof(LayoutElement));
            holder.layer = icon.gameObject.layer;
            holder.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)holder.transform;
            rect.SetParent(icon.transform.parent, false);
            rect.SetSiblingIndex(icon.transform.GetSiblingIndex() + 1);
            holder.SetActive(false);
            var made = new Made { Holder = rect };
            for (int i = 0; i < Corners.Length; i++)
            {
                made.Rings[i] = Part(rect, "socket" + i, SocketArt.Ring, Corners[i] / Units, (Corners[i] + Vector2.one * Pip) / Units);
                made.Gems[i] = Part(made.Rings[i].rectTransform, "gem", SocketArt.Gem, Vector2.zero, Vector2.one);
            }
            return made;
        }

        private static Image Part(RectTransform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The icon's anchors, pivot, offsets and scale; written only when they differ.</summary>
        private static void Fit(RectTransform holder, RectTransform icon)
        {
            if (holder.anchorMin != icon.anchorMin || holder.anchorMax != icon.anchorMax || holder.pivot != icon.pivot)
            {
                holder.anchorMin = icon.anchorMin;
                holder.anchorMax = icon.anchorMax;
                holder.pivot = icon.pivot;
            }
            if (holder.offsetMin != icon.offsetMin || holder.offsetMax != icon.offsetMax)
            {
                holder.offsetMin = icon.offsetMin;
                holder.offsetMax = icon.offsetMax;
            }
            if (holder.localScale != icon.localScale)
            {
                holder.localScale = icon.localScale;
            }
        }
    }
}

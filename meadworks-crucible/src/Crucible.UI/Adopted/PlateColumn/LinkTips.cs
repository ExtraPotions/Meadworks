// Adopted from ValheimMods (https://github.com/geraldjglasgow/ValheimMods), shipped with PackPanel 0.12.0.
// Upstream: ValheimModLibs/PlateColumn/LinkTips.cs at 01821b8557e4a77e37f5433c885b1f122cae8249.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later; see THIRD_PARTY_NOTICES/ValheimMods-GPL-3.0-or-later.txt.
// Modified by expDARE for Crucible (Meadworks), 2026-10-06: adoption header added and namespace moved
// under ExtraPotions.Meadworks.Crucible.UI.PlateColumn; the code is otherwise unchanged (tools/packpanel-ui-provenance.py).
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ExtraPotions.Meadworks.Crucible.UI.PlateColumn
{
    /// <summary>
    /// Tooltips on words: hovering a <c>&lt;link="id"&gt;</c> in a TextMeshPro text shows the box the column's tips show
    /// (<see cref="TipTemplate"/>: the game's inventory item tooltip with the gold border) beside the word, a moment after
    /// the pointer reaches it, until the pointer leaves the word. The text marks the words with link tags and
    /// <see cref="Set"/> gives each id its topic and text. Follows the pointer through the UI's own pointer events, so the
    /// text must take raycasts (<see cref="On"/> turns that on). Local only; nothing is sent.
    /// </summary>
    public sealed class LinkTips : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private const float ShowDelay = 0.3f;
        private const float LookSeconds = 0.25f;

        private readonly Dictionary<string, KeyValuePair<string, string>> tips = new Dictionary<string, KeyValuePair<string, string>>();
        private GameObject? prefab;
        private TMP_Text? text;
        private Camera? eventCamera;
        private Vector2 pointer;
        private bool inside;
        private string hovered = "";
        private float showAt = -1f;
        private GameObject? box;

        // The link under the pointer as last looked for, and what for: it is looked for again only when the pointer
        // moved, the text or the tips changed, or a quarter second passed (the mesh may follow a new text a frame
        // late), and a link's id (a new string each time it is read) is read again only for another link or a new look.
        private bool moved;
        private string? seenText;
        private float nextLook;
        private int underIndex = -1;
        private string underId = "";
        private string under = "";

        /// <summary>The text's word tips, added the first time. Null when the game's item tooltip cannot be found.</summary>
        public static LinkTips? On(InventoryGui gui, TMP_Text text)
        {
            GameObject? template = TipTemplate.For(gui);
            if (template == null || text == null)
            {
                return null;
            }
            LinkTips tips = text.GetComponent<LinkTips>();
            if (tips == null)
            {
                tips = text.gameObject.AddComponent<LinkTips>();
            }
            tips.prefab = template;
            tips.text = text;
            text.raycastTarget = true;
            return tips;
        }

        /// <summary>The tip for the words tagged <c>&lt;link="id"&gt;</c>.</summary>
        public void Set(string id, string topic, string words)
        {
            tips[id] = new KeyValuePair<string, string>(topic, words);
            moved = true;
        }

        /// <summary>Forgets every tip and hides the one showing; for text about to be rewritten.</summary>
        public void Clear()
        {
            tips.Clear();
            moved = true;
            Hide();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            inside = true;
            Track(eventData);
        }

        public void OnPointerMove(PointerEventData eventData) => Track(eventData);

        public void OnPointerExit(PointerEventData eventData)
        {
            inside = false;
            Hide();
        }

        private void Track(PointerEventData eventData)
        {
            pointer = eventData.position;
            eventCamera = eventData.enterEventCamera;
            moved = true;
        }

        private void Update()
        {
            string id = inside ? Under() : "";
            if (id != hovered)
            {
                Hide();
                hovered = id;
                showAt = id.Length > 0 ? Time.unscaledTime + ShowDelay : -1f;
            }
            if (showAt >= 0f && Time.unscaledTime >= showAt)
            {
                showAt = -1f;
                Show();
            }
        }

        /// <summary>The id of the link under the pointer that has a tip, "" for none; the last answer until something
        /// it depends on changed.</summary>
        private string Under()
        {
            if (text == null)
            {
                return "";
            }
            string current = text.text;
            bool fresh = !ReferenceEquals(current, seenText) || Time.unscaledTime >= nextLook;
            if (!moved && !fresh)
            {
                return under;
            }
            moved = false;
            seenText = current;
            nextLook = Time.unscaledTime + LookSeconds;
            under = LinkAt(text, fresh);
            return under;
        }

        private string LinkAt(TMP_Text text, bool fresh)
        {
            int index = TMP_TextUtilities.FindIntersectingLink(text, pointer, eventCamera);
            if (index < 0 || index >= text.textInfo.linkCount)
            {
                underIndex = -1;
                return "";
            }
            if (index != underIndex || fresh)
            {
                underIndex = index;
                underId = text.textInfo.linkInfo[index].GetLinkID();
            }
            return tips.ContainsKey(underId) ? underId : "";
        }

        private void Show()
        {
            if (text == null || !tips.TryGetValue(hovered, out KeyValuePair<string, string> tip))
            {
                return;
            }
            Vector3[]? corners = LinkBounds.Of(text, hovered);
            if (corners != null)
            {
                UITooltip.HideTooltip();
                box = TipBox.Show(prefab, text.canvas, corners, tip.Key, tip.Value);
            }
        }

        private void OnDisable()
        {
            inside = false;
            Hide();
        }

        private void Hide()
        {
            hovered = "";
            showAt = -1f;
            if (box != null)
            {
                Destroy(box);
                box = null;
            }
        }
    }
}

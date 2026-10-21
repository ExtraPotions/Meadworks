using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ExtraPotions.Mantle.Embedded.PlateColumn
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

        private readonly Dictionary<string, KeyValuePair<string, string>> tips = new Dictionary<string, KeyValuePair<string, string>>();
        private GameObject? prefab;
        private TMP_Text? text;
        private Camera? eventCamera;
        private Vector2 pointer;
        private bool inside;
        private string hovered = "";
        private float showAt = -1f;
        private GameObject? box;

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
        public void Set(string id, string topic, string words) => tips[id] = new KeyValuePair<string, string>(topic, words);

        /// <summary>Forgets every tip and hides the one showing; for text about to be rewritten.</summary>
        public void Clear()
        {
            tips.Clear();
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
        }

        private void Update()
        {
            string id = inside ? LinkAt() : "";
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

        /// <summary>The id of the link under the pointer that has a tip, "" for none.</summary>
        private string LinkAt()
        {
            if (text == null)
            {
                return "";
            }
            int index = TMP_TextUtilities.FindIntersectingLink(text, pointer, eventCamera);
            if (index < 0 || index >= text.textInfo.linkCount)
            {
                return "";
            }
            string id = text.textInfo.linkInfo[index].GetLinkID();
            return tips.ContainsKey(id) ? id : "";
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

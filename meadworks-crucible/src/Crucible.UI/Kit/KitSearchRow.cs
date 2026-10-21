using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>One square button of a <see cref="KitSearchRow"/>, listed from the row's right end.</summary>
    public sealed class KitSearchButton
    {
        public string Id;
        public KitGlyph Glyph;
        public Action<KitClickKind> Click;
        public string Topic = "";
        public string Tip = "";
    }

    /// <summary>
    /// KX03: a row of <see cref="KitSearchLayout.Height"/> along the top of a list rect (a top-left anchored panel, as the
    /// game's recipe list): a search <see cref="KitTextField"/> and right-aligned square <see cref="KitIconButton"/>s
    /// (26, 6 apart, centered vertically). While shown the list gives up the row plus a gap (34) and gets it back when the
    /// row turns off; <see cref="Tick"/> re-reads the list's size every update, so a mod that resizes the panel widens
    /// the field too (<see cref="KitListShrink"/>). When the list is missing or not top-left anchored, the row warns once
    /// and stays off for the session (<see cref="KitLatch"/>), leaving the game's list untouched. Null on a headless process.
    /// </summary>
    public sealed class KitSearchRow
    {
        private readonly KitListShrink shrink = new KitListShrink();
        private readonly List<KitIconButton> buttons = new List<KitIconButton>();

        public RectTransform Row { get; }
        public RectTransform List { get; }
        public KitTextField Field { get; }
        public IReadOnlyList<KitIconButton> Buttons => buttons;

        /// <summary>The search text is cleared when the host panel closes (<see cref="Closed"/>).</summary>
        public bool ClearOnClose { get; set; }

        public bool Shown => Row != null && Row.gameObject.activeSelf;

        /// <summary>The row set the list's size (the product mirrors it, for example into the game's list base size).</summary>
        public event Action<Vector2> ListWritten;

        private KitSearchRow(RectTransform row, RectTransform list, KitTextField field)
        {
            Row = row;
            List = list;
            Field = field;
        }

        /// <summary>
        /// The row over <paramref name="list"/>, built hidden; <paramref name="latch"/> is the product's session latch
        /// (a failure warns once through it and later calls return null without a word). Null headless or on failure.
        /// </summary>
        public static KitSearchRow Attach(RectTransform list, string name, IList<KitSearchButton> buttons, KitLatch latch, string placeholder = null)
        {
            if (!CrucibleUiPlugin.Active || latch == null || latch.Failed)
                return null;
            if (list == null || list.parent == null || !TopLeft(list))
            {
                if (latch.Fail())
                    CrucibleUiPlugin.Log?.Warning($"Crucible.UI: search row {name}: the list was not found or is not top-left anchored; the row is off for this session");
                return null;
            }
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = list.gameObject.layer;
            RectTransform row = (RectTransform)go.transform;
            row.SetParent(list.parent, false);
            row.SetSiblingIndex(list.GetSiblingIndex() + 1);
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = list.anchoredPosition;
            row.sizeDelta = new Vector2(list.sizeDelta.x, KitSearchLayout.Height);
            int count = buttons?.Count ?? 0;
            KitTextField field = KitTextField.Create(row, name + "_field", KitFieldMode.Search, Vector2.zero,
                new Vector2(KitSearchLayout.FieldWidth(row.sizeDelta.x, count), KitSearchLayout.Height), placeholder);
            if (field == null)
            {
                KitUnity.Retire(go);
                return null;
            }
            KitSearchRow made = new KitSearchRow(row, list, field);
            for (int i = 0; i < count; i++)
            {
                KitSearchButton spec = buttons[i];
                KitIconButton button = KitIconButton.Create(row, name + "_" + spec.Id, spec.Glyph,
                    new Vector2(KitSearchLayout.ButtonX(row.sizeDelta.x, i), KitSearchLayout.ButtonY), KitSearchLayout.Button, spec.Click);
                if (button == null)
                    continue;
                if (!string.IsNullOrEmpty(spec.Tip))
                    button.Tooltip(spec.Topic, spec.Tip);
                made.buttons.Add(button);
            }
            go.SetActive(false);
            return made;
        }

        /// <summary>Every update of the host panel: shown or hidden, the list sized to match, the row following its size.</summary>
        public void Tick(bool show)
        {
            if (Row == null || List == null)
                return;
            if (Row.gameObject.activeSelf != show)
                Row.gameObject.SetActive(show);
            KitShrinkResult result = shrink.Apply(KitUnity.Vector(List.anchoredPosition), KitUnity.Vector(List.sizeDelta), show);
            Row.anchoredPosition = KitUnity.Vector(result.RowPosition);
            Row.sizeDelta = KitUnity.Vector(result.RowSize);
            if (result.Write)
            {
                List.anchoredPosition = KitUnity.Vector(result.ListPosition);
                List.sizeDelta = KitUnity.Vector(result.ListSize);
                try
                {
                    ListWritten?.Invoke(List.sizeDelta);
                }
                catch (Exception e)
                {
                    CrucibleUiPlugin.Log?.Error($"Crucible.UI: search row {Row.name} list size handler failed: {e}");
                }
            }
            if (show)
                Layout(result.RowSize.X);
        }

        /// <summary>The host panel closed: the text goes when <see cref="ClearOnClose"/> says so.</summary>
        public void Closed()
        {
            if (ClearOnClose)
                Field?.Clear();
        }

        /// <summary>The search key or the pad's entry point: the cursor in the field, or Steam's keyboard.</summary>
        public KitKeyboardOutcome Focus() => Shown && Field != null ? Field.Focus() : KitKeyboardOutcome.Focused;

        /// <summary>Gives the list its full size back and removes the row.</summary>
        public void Detach()
        {
            if (List != null && Row != null)
                Tick(false);
            Field?.Destroy();
            if (Row != null)
                KitUnity.Retire(Row.gameObject);
        }

        private void Layout(float width)
        {
            if (Field?.Rect != null)
                Field.Rect.sizeDelta = new Vector2(KitSearchLayout.FieldWidth(width, buttons.Count), KitSearchLayout.Height);
            for (int i = 0; i < buttons.Count; i++)
                if (buttons[i]?.Button != null)
                    ((RectTransform)buttons[i].Button.transform).anchoredPosition = new Vector2(KitSearchLayout.ButtonX(width, i), KitSearchLayout.ButtonY);
        }

        private static bool TopLeft(RectTransform rect)
        {
            Vector2 corner = new Vector2(0f, 1f);
            return rect.anchorMin == corner && rect.anchorMax == corner && rect.pivot == corner;
        }
    }
}

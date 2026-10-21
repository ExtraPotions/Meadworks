// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>The Rune Table uses the shared framed tooltip beside its hovered control.</summary>
    internal static class HoverBox
    {
        private static GameObject? _prefab;
        private static TipHover? _owner;

        public static void Remember(GameObject? prefab)
        {
            if (_prefab == null && prefab != null) _prefab = prefab;
        }

        public static void Show(TipHover owner)
        {
            Hide();
            var native = owner.GetComponent<UITooltip>();
            if (native == null && _prefab != null)
            {
                native = owner.gameObject.AddComponent<UITooltip>();
                native.m_tooltipPrefab = _prefab;
            }
            _owner = owner;
            if(owner.Structured!=null)KitStructuredTip.Show(owner.gameObject,owner.Structured,owner.Topic,owner.Text);
            else KitTooltip.Show(owner.gameObject, owner.Topic, owner.Text);
        }

        public static void Refresh(TipHover owner)
        {
            if (_owner != owner) return;
            if (owner.Empty) { Hide(); return; }
            if(owner.Structured!=null)KitStructuredTip.Set(owner.gameObject,owner.Structured,owner.Topic,owner.Text);
            else KitTooltip.Set(owner.gameObject, owner.Topic, owner.Text);
        }

        public static void Hide(TipHover? owner = null)
        {
            if (owner != null && owner != _owner) return;
            if (_owner != null) ExtraPotions.Meadworks.Crucible.UI.KitTooltip.Hide(_owner.gameObject);
            _owner = null;
        }
    }
    /// <summary>A hover text on a Rune Table element, shown in the <see cref="HoverBox"/>.</summary>
    internal sealed class TipHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Topic { get; private set; } = "";
        public string Text { get; private set; } = "";
        public KitTipDocument? Structured { get; private set; }
        public bool Empty => Topic.Length == 0 && Text.Length == 0;

        public void Set(string topic, string text)
        {
            if (topic == Topic && text == Text && Structured == null)
            {
                return;
            }
            Structured = null;
            Topic = topic;
            Text = text;
            HoverBox.Refresh(this);
        }

        public void SetStructured(KitTipDocument document,string topic,string text)
        {
            if(Structured?.Key==document.Key && Topic==topic && Text==text)return;
            Structured=document;Topic=topic;Text=text;HoverBox.Refresh(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!Empty)
            {
                HoverBox.Show(this);
            }
        }

        public void OnPointerExit(PointerEventData eventData) => HoverBox.Hide(this);

        private void OnDisable() => HoverBox.Hide(this);
    }
}

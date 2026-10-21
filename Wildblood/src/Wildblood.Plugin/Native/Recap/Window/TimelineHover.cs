// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// Hovering the timeline shows a small picture of the clip at that point and its time, just above the pointer, as a
    /// video player does. The picture has a texture of its own, decoded only when the hovered frame changes.
    /// </summary>
    internal sealed class TimelineHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private Func<DeathRecap?> _recap = () => null;
        private RectTransform _track = null!;
        private RectTransform _popup = null!;
        private RawImage _picture = null!;
        private TMP_Text _label = null!;
        private Texture2D _texture = null!;
        private DeathRecap? _shownRecap;
        private int _shown = -1;

        public void Init(Func<DeathRecap?> recap, RectTransform track, RectTransform popup, RawImage picture, TMP_Text label)
        {
            _recap = recap;
            _track = track;
            _popup = popup;
            _picture = picture;
            _label = label;
            _texture = RecapPictures.NewTexture("ecr_recap_preview");
            _picture.texture = _texture;
            _popup.gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData data) => Follow(data);

        public void OnPointerMove(PointerEventData data) => Follow(data);

        public void OnPointerExit(PointerEventData data) => _popup.gameObject.SetActive(false);

        private void OnDisable()
        {
            if (_popup != null)
            {
                _popup.gameObject.SetActive(false);
            }
        }

        private void OnDestroy() => Destroy(_texture);

        private void Follow(PointerEventData data)
        {
            DeathRecap? recap = _recap();
            if (recap == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, data.position, null, out Vector2 local))
            {
                _popup.gameObject.SetActive(false);
                return;
            }
            Rect track = _track.rect;
            float share = Mathf.Clamp01((local.x - track.xMin) / Mathf.Max(1f, track.width));
            float time = share * recap.Duration;
            _label.text = RecapText.Clip(time);
            Picture(recap, time);
            Place(new Vector3(Mathf.Lerp(track.xMin, track.xMax, share), track.yMax, 0f));
            _popup.gameObject.SetActive(true);
            _popup.SetAsLastSibling();
        }

        private void Picture(DeathRecap recap, float time)
        {
            _picture.enabled = recap.HasVideo;
            if (!recap.HasVideo)
            {
                return;
            }
            int index = Mathf.Max(0, recap.FrameAt(time));
            if ((index != _shown || recap != _shownRecap) && RecapPictures.Show(_texture, recap.Frames[index]))
            {
                _shown = index;
                _shownRecap = recap;
            }
        }

        // Centred over the pointer's place on the track, just above it, in the popup's parent's space.
        private void Place(Vector3 onTrack)
        {
            Vector3 world = _track.TransformPoint(onTrack);
            RectTransform parent = (RectTransform)_popup.parent;
            Vector2 local = parent.InverseTransformPoint(world);
            _popup.anchorMin = _popup.anchorMax = new Vector2(0.5f, 0.5f);
            _popup.pivot = new Vector2(0.5f, 0f);
            _popup.anchoredPosition = local - parent.rect.center + new Vector2(0f, 10f);
        }
    }
}

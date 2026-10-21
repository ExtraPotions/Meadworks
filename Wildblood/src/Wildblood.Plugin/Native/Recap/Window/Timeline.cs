// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Wildblood.Adopted.Recap.Window
{
    /// <summary>
    /// The clip's timeline: a copy of the game's own slider (the stack split dialog's), dragged or clicked to move through
    /// the clip, with a small mark at every hit and a red one at the moment of death. Following the playback never
    /// counts as a move, so only the player's own drags seek.
    /// </summary>
    internal sealed class Timeline
    {
        private static readonly Color HitColour = new Color(1f, 0.62f, 0.2f, 0.95f);
        private static readonly Color DeathColour = new Color(0.9f, 0.12f, 0.1f, 1f);

        private readonly Slider _slider;
        private readonly RectTransform _marks;
        private DeathRecap? _recap;

        /// <summary>Asked to show this clip time.</summary>
        public Action<float>? Seek;

        public Timeline(Slider slider, RectTransform marks)
        {
            _slider = slider;
            _marks = marks;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged = new Slider.SliderEvent();
            slider.onValueChanged.AddListener(Moved);
        }

        public void Sync(DeathRecap? recap, float time)
        {
            if (recap != _recap)
            {
                Load(recap);
            }
            _slider.SetValueWithoutNotify(recap != null ? Mathf.Clamp01(time / recap.Duration) : 0f);
        }

        private void Moved(float value)
        {
            if (_recap != null)
            {
                Seek?.Invoke(value * _recap.Duration);
            }
        }

        private void Load(DeathRecap? recap)
        {
            _recap = recap;
            _slider.interactable = recap != null;
            foreach (Transform old in _marks)
            {
                UnityEngine.Object.Destroy(old.gameObject);
            }
            if (recap == null)
            {
                return;
            }
            for (int i = 0; i < recap.Hits.Count; i++)
            {
                Mark(recap.HitTime(i) / recap.Duration, HitColour, 3f, 0.7f);
            }
            Mark(recap.DeathAt / recap.Duration, DeathColour, 4f, 1.1f);
        }

        // A thin upright bar at a share of the track, as tall as a share of the slider.
        private void Mark(float at, Color colour, float width, float height)
        {
            RectTransform rect = UiParts.Node("ecr_recap_mark", _marks);
            float x = Mathf.Clamp01(at);
            rect.anchorMin = new Vector2(x, 0.5f - height / 2f);
            rect.anchorMax = new Vector2(x, 0.5f + height / 2f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
        }
    }
}

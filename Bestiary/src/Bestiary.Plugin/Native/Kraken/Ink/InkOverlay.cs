// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The ink layer on the local player's screen: a full-screen object in the HUD canvas right after the red damage
    /// flash, so the world view goes black while health, hotbar and the rest of the HUD stay drawn over it. Its splat
    /// images are built once and dealt out afresh on every hit (5 to 7, one big one near the middle and the rest in a
    /// ring round it, so together they blind): opaque for the hold, sliding down a little like running ink, then faded
    /// out and switched off. Switched off at once when the local player is gone or dead. Lives and dies with its HUD.
    /// </summary>
    internal class InkOverlay : MonoBehaviour
    {
        public const int Most = 7;
        private const int Least = 5;
        private const float Pop = 0.08f;
        private const float PopFrom = 0.85f;
        private const float Tilt = 35f;

        private readonly RawImage[] _splats = new RawImage[Most];
        private readonly float[] _drips = new float[Most];
        private CanvasGroup _group = null!;
        private float _hold, _fade, _time;

        /// <summary>Builds the layer and its images in the damage flash's parent, right after it, switched off.</summary>
        public static InkOverlay Create(Transform damageScreen)
        {
            var root = new GameObject("ECP_KrakenInk", typeof(RectTransform), typeof(CanvasGroup));
            root.SetActive(false);
            root.layer = damageScreen.gameObject.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(damageScreen.parent, false);
            rect.SetSiblingIndex(damageScreen.GetSiblingIndex() + 1);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Adapters.NativeRuntime.Own(root);
            InkOverlay overlay = root.AddComponent<InkOverlay>();
            overlay.Build(root.GetComponent<CanvasGroup>());
            return overlay;
        }

        /// <summary>Deals a fresh set of splats, held <paramref name="hold"/> seconds, then faded over <paramref name="fade"/>.</summary>
        public void Show(Texture2D[] textures, float hold, float fade)
        {
            if (textures.Length == 0)
            {
                return;
            }
            _hold = hold;
            _fade = fade;
            _time = 0f;
            _group.alpha = 1f;
            gameObject.SetActive(true);
            Deal(textures, Area());
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead())
            {
                Hide();
                return;
            }
            float dt = Time.deltaTime;
            _time += dt;
            if (_time >= _hold + _fade)
            {
                Hide();
                return;
            }
            _group.alpha = _time <= _hold ? 1f : 1f - (_time - _hold) / _fade;
            Run(dt);
        }

        private void OnDestroy() => InkScreen.Forget(this);

        private void Build(CanvasGroup group)
        {
            _group = group;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            for (int i = 0; i < Most; i++)
            {
                _splats[i] = MakeSplat(i);
            }
        }

        private RawImage MakeSplat(int index)
        {
            var splat = new GameObject("Splat" + index, typeof(RectTransform), typeof(RawImage));
            splat.layer = gameObject.layer;
            var rect = (RectTransform)splat.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            RawImage image = splat.GetComponent<RawImage>();
            image.raycastTarget = false;
            splat.SetActive(false);
            return image;
        }

        private void Deal(Texture2D[] textures, Vector2 area)
        {
            int count = Random.Range(Least, Most + 1);
            float turn = Random.Range(0f, 360f);
            int first = Random.Range(0, textures.Length);
            for (int i = 0; i < Most; i++)
            {
                _splats[i].gameObject.SetActive(i < count);
                if (i < count)
                {
                    Place(i, count, turn, textures[(first + i) % textures.Length], area);
                }
            }
        }

        /// <summary>
        /// One splat: its texture, darkness, size, spot, tilt and drip speed. The size is the whole image's, 80% to 125%
        /// of the screen height; the ink fills a little over half of a splat image, so the ink itself spans about 45% to
        /// 70% of the screen height.
        /// </summary>
        private void Place(int index, int count, float turn, Texture2D texture, Vector2 area)
        {
            RawImage image = _splats[index];
            image.texture = texture;
            float shade = Random.Range(0.72f, 1f);
            image.color = new Color(shade, shade, shade, index == 0 ? 1f : Random.Range(0.9f, 1f));
            RectTransform rect = image.rectTransform;
            float side = area.y * (index == 0 ? Random.Range(1.1f, 1.25f) : Random.Range(0.8f, 1.1f));
            rect.sizeDelta = new Vector2(side, side);
            rect.anchoredPosition = Spot(index, count, turn, area);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-Tilt, Tilt));
            rect.localScale = new Vector3(PopFrom, PopFrom, 1f);
            _drips[index] = area.y * Random.Range(0.003f, 0.008f);
        }

        /// <summary>The first splat near the middle, the others spread round it on an ellipse the shape of the screen.</summary>
        private static Vector2 Spot(int index, int count, float turn, Vector2 area)
        {
            if (index == 0)
            {
                return new Vector2(Random.Range(-0.06f, 0.06f) * area.x, Random.Range(-0.05f, 0.05f) * area.y);
            }
            float angle = (turn + 360f * (index - 1) / (count - 1) + Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * area.x * Random.Range(0.26f, 0.4f);
            float y = Mathf.Sin(angle) * area.y * Random.Range(0.22f, 0.34f);
            return new Vector2(x, y);
        }

        /// <summary>The splats pop to full size as they land, then slide slowly down.</summary>
        private void Run(float dt)
        {
            float scale = Mathf.Lerp(PopFrom, 1f, _time / Pop);
            for (int i = 0; i < Most; i++)
            {
                RectTransform rect = _splats[i].rectTransform;
                if (rect.gameObject.activeSelf)
                {
                    rect.anchoredPosition += new Vector2(0f, -_drips[i] * dt);
                    rect.localScale = new Vector3(scale, scale, 1f);
                }
            }
        }

        /// <summary>The layer's size in canvas units; the screen's pixels if the canvas has not laid it out yet.</summary>
        private Vector2 Area()
        {
            Rect rect = ((RectTransform)transform).rect;
            return rect.height >= 1f ? rect.size : new Vector2(Screen.width, Screen.height);
        }
    }
}

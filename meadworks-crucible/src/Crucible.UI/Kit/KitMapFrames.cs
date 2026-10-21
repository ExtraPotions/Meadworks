// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Meadworks.Crucible.UI
{
    /// <summary>
    /// Meadworks Map Frames: our own frame images added as non-interactive overlay children of the game's minimap root (a thin wood border hugging the square minimap, outside its rect)
    /// and large-map root (a wood border around the whole map). Nothing of the game's textures is replaced, read back or saved; the map contents stay
    /// the game's. <see cref="KitMapFrameRules.Decide"/> picks build, keep or remove about twice a second, so switching the option off removes both
    /// overlays and the game's maps are as they were.
    /// </summary>
    public static class KitMapFrames
    {
        public const string SmallName = "Crucible_mapframe_small";
        public const string LargeName = "Crucible_mapframe_large";
        private static float next;

        /// <summary>The minimap frame while it is shown (so panels under the minimap can sit below its wood), else null.</summary>
        public static RectTransform ShownSmallFrame()
        {
            Minimap map = Minimap.instance;
            var frame = map != null && map.m_smallRoot != null ? map.m_smallRoot.transform.Find(SmallName) as RectTransform : null;
            return frame != null && frame.gameObject.activeInHierarchy ? frame : null;
        }

        /// <summary>The wood's drawn thickness of a frame image in its own local units.</summary>
        public static float WoodThickness(RectTransform frame)
        {
            Image wood = frame != null ? frame.GetComponent<Image>() : null;
            float multiplied = wood != null ? wood.pixelsPerUnit * wood.pixelsPerUnitMultiplier : 0f;
            return wood != null && wood.sprite != null && multiplied > 0f ? wood.sprite.border.x / multiplied : 0f;
        }

        public static void Update()
        {
            if (Time.unscaledTime < next)
                return;
            next = Time.unscaledTime + 0.5f;
            Minimap map = Minimap.instance;
            bool canBuild = MeadworksHeadless.AllowsUi && map != null && map.m_smallRoot != null && map.m_mapImageSmall != null && map.m_largeRoot != null && map.m_mapImageLarge != null;
            Apply(map, KitMapFrameKind.Small, canBuild);
            Apply(map, KitMapFrameKind.Large, canBuild);
        }

        private static void Apply(Minimap map, KitMapFrameKind kind, bool canBuild)
        {
            if (map == null)
                return;
            GameObject root = kind == KitMapFrameKind.Small ? map.m_smallRoot : map.m_largeRoot;
            string name = kind == KitMapFrameKind.Small ? SmallName : LargeName;
            Transform existing = root != null ? root.transform.Find(name) : null;
            switch (KitMapFrameRules.Decide(KitLook.MapFramesOn, canBuild, existing != null))
            {
                case KitMapFrameAction.Build:
                    Build(map, kind, root, name);
                    break;
                case KitMapFrameAction.Remove:
                    Object.Destroy(existing.gameObject);
                    break;
                case KitMapFrameAction.Keep:
                    Fit(map, kind, (RectTransform)existing);
                    break;
            }
        }

        private static void Build(Minimap map, KitMapFrameKind kind, GameObject root, string name)
        {
            // Both maps use the large map's nine-sliced wood; the minimap draws it thinner through the pixels-per-unit multiplier.
            Sprite sprite = KitArt.MapFrameLarge;
            if (sprite == null)
                return;
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root.transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = KitMapFrameRules.PixelsPerUnitMultiplier(kind);
            if (kind == KitMapFrameKind.Small)
                image.fillCenter = false;
            image.raycastTarget = false;
            rect.SetAsLastSibling();
            Fit(map, kind, rect);
        }

        // The overlay is centered on the game's map image (world position) and sized from that image's rect; re-fitted on every check so a resize follows.
        private static void Fit(Minimap map, KitMapFrameKind kind, RectTransform frame)
        {
            RectTransform image = (RectTransform)(kind == KitMapFrameKind.Small ? map.m_mapImageSmall : map.m_mapImageLarge).transform;
            RectTransform parent = (RectTransform)frame.parent;
            Vector3 scale = parent.lossyScale;
            Vector3 imageScale = image.lossyScale;
            float ratioX = scale.x != 0f ? imageScale.x / scale.x : 1f;
            float ratioY = scale.y != 0f ? imageScale.y / scale.y : 1f;
            // The wood's drawn thickness comes from the Image itself (sprite border over its multiplied pixels per unit, which
            // follows the canvas' reference pixels per unit), so the inner edge lands on the map picture with no dark gap.
            float thickness = WoodThickness(frame);
            if (thickness <= 0f) thickness = KitMapFrameRules.Thickness(kind);
            frame.sizeDelta = new Vector2(image.rect.width * ratioX + 2f * thickness, image.rect.height * ratioY + 2f * thickness);
            frame.position = image.TransformPoint(image.rect.center);
            frame.SetAsLastSibling();
        }
    }
}

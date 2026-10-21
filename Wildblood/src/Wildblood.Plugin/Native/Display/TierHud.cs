// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration. 2026-10-13: placed on the map picture.
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Wildblood.Runtime;
using TMPro;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Display;

internal static class TierHud
{
    static KitHudPanel? panel;
    static Transform? owner, minimap;
    static TMP_Text? label;
    const float Height = 18f, Inset = 4f;
    public static void Refresh() => WildbloodStatus.RefreshHud();
    // Karma only, as Solo draws it: small gold text inside the minimap's bottom-right corner. The world tier is Mantle's globe square right of the map.
    internal static void Draw(string karma, bool consumed)
    {
        Transform? root = Hud.instance != null ? Hud.instance.m_rootObject?.transform : null;
        Transform? map = Minimap.instance != null && Minimap.instance.m_smallRoot != null ? Minimap.instance.m_smallRoot.transform : null;
        // Inside the map picture itself: the small root reaches a few units past it, which put Karma over the sleep row below.
        RectTransform? picture = Minimap.instance != null && Minimap.instance.m_mapImageSmall != null ? Minimap.instance.m_mapImageSmall.rectTransform : null;
        if (panel != null && (panel.Root == null || owner != root || minimap != map)) Reset();
        if (consumed || karma.Length == 0 || !(root is RectTransform canvas) || !(map is RectTransform mini) || !map.gameObject.activeInHierarchy)
        { panel?.Update(false, false, 1f, 1f); return; }
        if (panel == null)
        {
            panel = KitHudPanel.Create(root, "WildbloodTierKarma", new Vector2(200, Height), Vector2.zero, global::ExtraPotions.Wildblood.Runtime.Plugin.Guid, "WildbloodTierKarma");
            if (panel == null) return;
            owner = root; minimap = map;
            label = KitAlmanacDialog.Label(panel.Content, "Karma", "", new Vector2(0, 0), new Vector2(200, Height), 12);
            label.alignment = TextAlignmentOptions.BottomRight;
            label.color = new Color(1f, .78f, .25f, 1f);
            label.richText = false; label.overflowMode = TextOverflowModes.Truncate;
        }
        // Measured in canvas units so a scaled minimap cannot push the text off screen.
        RectTransform area = picture != null ? picture : mini;
        Vector3 left = canvas.InverseTransformPoint(area.TransformPoint(new Vector3(area.rect.xMin, area.rect.yMin, 0)));
        Vector3 right = canvas.InverseTransformPoint(area.TransformPoint(new Vector3(area.rect.xMax, area.rect.yMin, 0)));
        float x = Mathf.Max(0f, left.x - canvas.rect.xMin + Inset);
        float width = Mathf.Max(0f, Mathf.Min(right.x - canvas.rect.xMin - Inset, canvas.rect.width) - x);
        panel.Root.sizeDelta = new Vector2(width, Height);
        label!.rectTransform.sizeDelta = new Vector2(width, Height);
        panel.SetPosition(new Vector2(x, canvas.rect.yMax - left.y - Height - Inset));
        if (label.text != karma) label.text = karma;
        panel.Update(true, false, 1f, 0f);
    }
    internal static void Reset() { panel?.Dispose(); panel = null; owner = minimap = null; label = null; }
}

// Edge - ExtraPotions Meadworks. SPDX-License-Identifier: GPL-3.0-only
// Portions from SecondaryAttacks 1.2.17 (sighsorry1029), GPL-3.0, at 4375a2b17297052a773c5b143b0cae21dc655ed8.
// SecondaryCooldownHudSystem.cs: native entry collection and sorting only. Source notices retained in THIRD_PARTY_NOTICES.
// Changed by expDARE, 2026: kit panel and meters, preset names/icons and C05 guarded ownership.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
// The inventory preview belongs beneath Mantle's equipment panel; the separate HUD retains its saved layout.
// Contents refresh at most ten times a second. Both hosts use the existing forged side plate.
using System;
using System.Collections.Generic;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Secondaries;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Edge.Runtime.Secondaries.Native;

internal static class EdgeCooldownHud
{
    private const int Columns = 5, PreviewSlotCount = 10;
    // Pad: clearance for the side plate's iron edge and corner brackets.
    private const float CellWidth = 76, CellHeight = 68, RefreshSeconds = .1f, Pad = 6;
    private static readonly List<NativeCooldownEntry> Entries = new();
    private static readonly List<Slot> Slots = new();
    private static readonly Comparison<NativeCooldownEntry> ByOrder = (left, right) =>
    {
        int modeCompare = left.SortOrder.CompareTo(right.SortOrder);
        return modeCompare != 0 ? modeCompare : left.Remaining.CompareTo(right.Remaining);
    };
    private static KitHudPanel? panel;
    private static TMP_Text? title;
    private static Transform? hudParent;
    private static Vector2 lastParentSize;
    private static RectTransform? previewRoot, content;
    private static int count;
    private static float nextRefresh;
    private static bool lastPreview, previewRead, previewVisible;

    internal static void Update(Player? player)
    {
        var settings = Plugin.Settings;
        var root = Hud.instance?.m_rootObject;
        if (!Plugin.AllowsUi || settings == null || player == null || player != Player.m_localPlayer ||
            !EdgeModules.Active(EdgeModule.Secondaries) || !settings.Get(EdgeSettings.Interface.CooldownHud) ||
            root == null || !root.activeInHierarchy || player.IsDead())
        { Hide(); return; }
        var gui = InventoryGui.instance;
        var facts = MeadworksInput.Facts();
        var place = CooldownPanelPlacement.Place(
            facts.MenuVisible || facts.ConsoleVisible || facts.ModWindowOpen || facts.TypingWindowOpen || facts.OtherGameUiVisible ||
            Game.IsPaused() || (TextViewer.instance != null && TextViewer.instance.IsVisible()),
            gui != null && InventoryGui.IsVisible(),
            gui != null && (gui.m_player == null || gui.IsSkillsPanelOpen || gui.IsTextPanelOpen || gui.IsTrophisPanelOpen));
        if (place == CooldownPanelPlace.Hidden) { Hide(); return; }
        bool preview = place == CooldownPanelPlace.InventoryPreview;
        // Mantle owns this exact direct child; no dependency on its assembly or changes to its layout.
        var equipment = preview && gui?.m_player != null ? gui.m_player.Find("Mantle_slots") as RectTransform : null;
        // Vanilla equipment lives in the player grid; only standalone Edge uses that panel as the fallback.
        if (preview && equipment == null && !BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.meadworks.mantle"))
            equipment = gui?.m_player;
        if (preview && (equipment == null || !equipment.gameObject.activeInHierarchy)) { Hide(); return; }
        bool created = false;
        if (panel == null || panel.Root == null || content == null || hudParent != root.transform)
        {
            Dispose(); hudParent = root.transform;
            panel = KitHudPanel.Create(hudParent, "Edge_Cooldowns", PanelSize(1), Vector2.zero, global::ExtraPotions.Edge.Runtime.Plugin.Guid, "Edge_Cooldowns");
            if (panel == null) return;
            KitForged.SidePlate(panel.Root); // the iron-cornered plate other Meadworks side panels wear
            content = new GameObject("CooldownContent", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(panel.Content, false);
            Place(content, Vector2.zero, PanelSize(1));
            title = KitCanvas.Text(content, "Title", "", 12, Color.white, TextAlignmentOptions.Left, false);
            Place(title.rectTransform, new Vector2(7 + Pad, 4 + Pad), new Vector2(Columns * CellWidth - Pad, 19));
            created = true;
        }
        if (preview && previewRoot == null)
        {
            previewRoot = KitPanel.Create(equipment!, "Edge_ReadyPreview", PanelSize(2));
            if (previewRoot == null) { Hide(); return; }
            KitForged.SidePlate(previewRoot);
            var background = KitPanel.Background(previewRoot);
            background.raycastTarget = false;
            var alpha = background.GetComponent<CanvasGroup>() ?? background.gameObject.AddComponent<CanvasGroup>();
            alpha.alpha = .8f; alpha.blocksRaycasts = false;
        }
        var host = preview ? previewRoot! : panel.Root;
        if (content!.parent != host)
        {
            content.SetParent(host, false);
            Place(content, Vector2.zero, host.sizeDelta);
        }
        float now = Time.unscaledTime;
        // The entries, their order and the slot texts at most ten times a second (every frame costs about 0.3 ms); at once
        // when the panel is new or switches between the HUD and the inventory preview.
        if (created || preview != lastPreview || now >= nextRefresh)
        {
            lastPreview = preview;
            nextRefresh = now + RefreshSeconds;
            Refresh(player, preview);
        }
        // The inventory preview follows the HUD panel's saved visibility, which is read by presenting the panel. That is done
        // once when the preview opens, not every frame: showing and hiding the panel each frame rebuilt its canvas and the
        // HUD's every frame the inventory was open (playtest 2026-10-20, item 25).
        bool visible;
        if (!preview || !previewRead)
        {
            panel.Update(count > 0, false, 1, .8f);
            visible = panel.Root.gameObject.activeSelf;
            previewRead = preview;
            previewVisible = visible;
        }
        else visible = previewVisible;
        if (previewRoot != null) previewRoot.gameObject.SetActive(preview && visible);
        if (preview)
        {
            // Read saved visibility from the registered HUD, then hide it before the frame renders.
            panel.Update(false, false, 1, .8f);
            if (previewRoot!.parent != equipment) previewRoot.SetParent(equipment, false);
            previewRoot.anchorMin = previewRoot.anchorMax = new Vector2(.5f, 0);
            previewRoot.pivot = new Vector2(.5f, 1);
            previewRoot.localScale = Vector3.one;
            var back = KitPanel.Background(previewRoot).rectTransform;
            float overhang = Mathf.Max(0, previewRoot.InverseTransformPoint(back.TransformPoint(new Vector3(0, back.rect.yMax))).y - previewRoot.rect.yMax);
            var equipmentBack = KitPanel.Background(equipment!);
            float equipmentOverhang = equipmentBack != null
                ? Mathf.Max(0, equipment!.rect.yMin - equipment.InverseTransformPoint(equipmentBack.rectTransform.TransformPoint(new Vector3(0, equipmentBack.rectTransform.rect.yMin))).y)
                : 0;
            previewRoot.anchoredPosition = new Vector2(0, -Pad - overhang - equipmentOverhang);
            return;
        }
        if (count == 0) return;
        var parentSize = panel.Root.parent is RectTransform rect ? rect.rect.size : new Vector2(Screen.width, Screen.height);
        if (parentSize != lastParentSize)
        { panel.SetPosition(Vector2.Scale(new Vector2(0.615f, 0.22f), parentSize)); lastParentSize = parentSize; }
    }

    private static void Refresh(Player player, bool preview)
    {
        Entries.Clear();
        SneakAmbushChargeSystem.CollectHudEntries(player, Entries);
        SecondaryCooldownGroupSystem.CollectHudEntries(player, Entries);
        Entries.Sort(ByOrder);
        count = Mathf.Max(Entries.Count, preview ? PreviewSlotCount : 0);
        var size = PanelSize(Mathf.CeilToInt(count / (float)Columns));
        var host = preview ? previewRoot! : panel!.Root;
        if (host.sizeDelta != size) { host.sizeDelta = size; KitPanel.Refresh(host); }
        content!.sizeDelta = size;
        if (count == 0) return;
        string hint = EdgeText.Text("hint_secondary");
        if (title!.text != hint) title.text = hint;
        UpdateSlots(count);
    }

    private static Vector2 PanelSize(int rows) => new(Columns * CellWidth + 12 + 2 * Pad, 26 + rows * CellHeight + 2 * Pad);

    private static void UpdateSlots(int count)
    {
        while (Slots.Count < count) Slots.Add(new Slot(content!, Slots.Count));
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i].Root.gameObject.activeSelf != i < count) Slots[i].Root.gameObject.SetActive(i < count);
            if (i >= count) continue;
            if (i < Entries.Count) Slots[i].Set(Entries[i]);
            else Slots[i].Preview(PresetCatalog.Entries[i % PresetCatalog.Entries.Count]);
        }
    }

    private static void Hide()
    {
        previewRead = false;
        panel?.Update(false, false, 1, .8f);
        if (previewRoot != null) previewRoot.gameObject.SetActive(false);
    }
    internal static void Dispose()
    {
        count = 0; lastPreview = previewRead = false; nextRefresh = 0;
        foreach (var slot in Slots) slot.Meter.Dispose();
        Slots.Clear(); Entries.Clear(); panel?.Dispose(); panel = null; title = null; hudParent = null;
        if (previewRoot != null) { previewRoot.gameObject.SetActive(false); UnityEngine.Object.Destroy(previewRoot.gameObject); }
        previewRoot = null; content = null; lastParentSize = Vector2.zero;
    }
    private static void Place(RectTransform rect, Vector2 at, Vector2 size)
    { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(at.x, -at.y); rect.sizeDelta = size; }
    private sealed class Slot
    {
        internal readonly RectTransform Root;
        internal readonly KitMeter Meter;
        private readonly Image icon;
        private readonly TMP_Text name;
        internal Slot(RectTransform parent, int index)
        {
            Root = new GameObject("Preset" + index, typeof(RectTransform)).GetComponent<RectTransform>();
            Root.SetParent(parent, false);
            Place(Root, new Vector2(6 + Pad + index % Columns * CellWidth, 25 + Pad + index / Columns * CellHeight), new Vector2(CellWidth - 4, CellHeight));
            icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(Root, false); icon.preserveAspect = true; icon.raycastTarget = false;
            Place((RectTransform)icon.transform, new Vector2(24, 0), new Vector2(26, 26));
            name = KitCanvas.Text(Root, "Name", "", 10, Color.white, TextAlignmentOptions.Center, true);
            Place(name.rectTransform, new Vector2(0, 27), new Vector2(CellWidth - 4, 24));
            Meter = KitMeter.Create(Root, "Remaining", new Vector2(0, -53), new Vector2(CellWidth - 4, 12));
            Meter.Label.fontSize = 10;
        }
        // The last values written; a refresh writes only what changed.
        private Sprite? lastSprite;
        private Color lastColour;
        private string? lastName, lastText;
        private float lastValue = float.NaN, lastMaximum = float.NaN;
        internal void Set(NativeCooldownEntry entry)
        {
            Show(NativePresetPresentation.ResolveIcon(entry.PresetKey) ?? entry.Icon, Color.white,
                PresetCatalog.TryGet(entry.PresetKey, out var preset) ? EdgeText.Text(preset.NameKey) : EdgeText.Text("hint_secondary"));
            var text = entry.Mode == NativeCooldownFillMode.Charge ? entry.Badge : entry.Remaining.ToString("0.0") + " s";
            Fill(entry.Remaining, entry.Duration, text);
        }
        internal void Preview(PresetInfo preset)
        {
            Show(NativePresetPresentation.ResolveIcon(preset.Key), new Color(1, 1, 1, .45f), EdgeText.Text(preset.NameKey));
            Fill(0, 1, EdgeText.Text("hud_ready"));
        }
        private void Show(Sprite? sprite, Color colour, string text)
        {
            if (sprite != lastSprite || icon.enabled != (sprite != null)) { icon.sprite = sprite; icon.enabled = sprite != null; lastSprite = sprite; }
            if (colour != lastColour) { icon.color = colour; lastColour = colour; }
            if (text != lastName) { name.text = text; lastName = text; }
        }
        private void Fill(float value, float maximum, string text)
        {
            if (value == lastValue && maximum == lastMaximum && text == lastText) return;
            lastValue = value; lastMaximum = maximum; lastText = text;
            Meter.Set(value, maximum, text);
        }
    }
}

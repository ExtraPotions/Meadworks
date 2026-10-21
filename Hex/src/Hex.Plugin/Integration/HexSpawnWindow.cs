// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Original Hex presentation using the existing Crucible.UI virtual list, field, button and editor ownership.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Hex.Runtime.Native;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace ExtraPotions.Hex.Runtime;

internal static partial class HexWindow
{
    static IReadOnlyList<HexSpawnCatalog.Entry> catalog = Array.Empty<HexSpawnCatalog.Entry>();
    static readonly List<Selectable> catalogControls = new();
    static readonly List<GameObject> catalogObjects = new();
    static readonly Dictionary<RectTransform, List<Button>> catalogTiles = new();
    static KitTextField? catalogField;
    static KitToggle? catalogEnable, catalogKind;
    static TMP_Text? catalogCount;
    static string search = "", category = "";
    static bool catalogDirty, spawnCreatures;
    static bool CatalogTab => tab == "spawn";
    static void CatalogHeader()
    {
        try { catalog = spawnCreatures ? HexSpawnCatalog.Creatures() : HexSpawnCatalog.Items(); }
        catch { catalog = Array.Empty<HexSpawnCatalog.Entry>(); Result(HexText.Text("spawn_unsupported")); }
        category = "";
        catalogField = KitTextField.Create(window!.Panel, "CatalogSearch", KitFieldMode.Search, new Vector2(200, -60), new Vector2(360, 32), Ui("search"));
        if (catalogField != null)
        {
            Help(catalogField.Input.gameObject, Ui("search"), HexText.Text("help_catalog_search"));
            catalogField.Text = search; catalogField.Changed += SearchChanged;
            catalogControls.Add(catalogField.Input); window.Focusable(catalogField.Input, 1, 1);
        }
        var choose = KitButton.Create(window.Panel, "CatalogCategory", Ui("category"), () =>
        {
            var values = catalog.SelectMany(Categories).Distinct(StringComparer.Ordinal).OrderBy(CategoryName, StringComparer.CurrentCulture).Prepend("").ToArray();
            var names = values.Select(value => value.Length == 0 ? Ui("all") : CategoryName(value) + " (" + value + ")").ToArray();
            KitAlmanacChoice.Open(window, Ui("category"), names, Array.Empty<string>(), false, selected =>
            { var at = selected.Count == 1 ? Array.IndexOf(names, selected[0]) : -1; if (at >= 0) { category = values[at]; catalogDirty = true; } });
        }, new Vector2(580, -60), new Vector2(200, 32));
        var reload = KitButton.Create(window.Panel, "CatalogReload", Ui("reload"), () => { dirty = true; }, new Vector2(800, -60), new Vector2(150, 32));
        Help(choose.gameObject, Ui("category"), HexText.Text("help_catalog_category")); Help(reload.gameObject, Ui("reload"), HexText.Text("help_catalog_reload"));
        catalogControls.Add(choose); catalogControls.Add(reload); catalogObjects.Add(choose.gameObject); catalogObjects.Add(reload.gameObject);
        window.Focusable(choose, 2, 1); window.Focusable(reload, 3, 1);
        catalogEnable = KitToggle.Create(window.Panel, "CatalogEnabled", Plugin.Settings!.Spawning.Value, value => Plugin.Settings.SetPreference("spawning", value),
            new Vector2(200, -104), new Vector2(180, 30), Ui("on"), Ui("off"));
        catalogEnable.Tooltip(Label("spawning"), HexText.Text("spawning"));
        catalogControls.Add(catalogEnable.Button); window.Focusable(catalogEnable.Button, 1, 2);
        // One Spawn page: this switch picks the items or the creatures list (formerly two pages).
        catalogKind = KitToggle.Create(window.Panel, "CatalogKind", spawnCreatures, value => { spawnCreatures = value; dirty = true; },
            new Vector2(390, -104), new Vector2(180, 30), Ui("creatures"), Ui("items"));
        catalogKind.Tooltip(Ui("spawn"), HexText.Text("help_spawn_kind"));
        catalogControls.Add(catalogKind.Button); window.Focusable(catalogKind.Button, 2, 2);
        catalogCount = KitAlmanacDialog.Label(window.Panel, "CatalogCount", "", new Vector2(580, -104), new Vector2(370, 34), 14);
        catalogObjects.Add(catalogCount.gameObject);
        KitAlmanacDialog.Place(list!.Root, new Vector2(200, -148), new Vector2(750, 462));
        DrawCatalog();
    }
    static void SearchChanged(string value) { search = value; catalogDirty = true; }
    static IEnumerable<string> Categories(HexSpawnCatalog.Entry entry) => entry.Category.Split(',').Select(value => value.Trim()).Where(value => value.Length > 0);
    static string CategoryName(string value)
    {
        if (value == "Unclassified") return Ui("unclassified");
        var token = "$" + (spawnCreatures ? "biome_" : "itemtype_") + value.ToLowerInvariant();
        var localized = Localization.instance?.Localize(token) ?? token;
        return localized == token || localized == "[" + token.TrimStart('$') + "]" ? value : localized;
    }
    static void TickCatalog()
    {
        if (!CatalogTab) return;
        if (catalogDirty) DrawCatalog();
        catalogEnable?.SetValue(Plugin.Settings!.Spawning.Value);
        catalogEnable?.SetLabels(Ui("on"), Ui("off"));
        catalogKind?.SetValue(spawnCreatures); catalogKind?.SetLabels(Ui("creatures"), Ui("items"));
        catalogEnable?.SetUsable(Plugin.Settings!.Enabled.Value && HexMovement.Owned(Player.m_localPlayer)
            && MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Economy).Allowed);
    }
    static void DrawCatalog()
    {
        catalogDirty = false;
        if (list == null || !CatalogTab) return;
        var filtered = catalog.Where(entry => entry.Prefab != null && entry.Matches(search) && (category.Length == 0 || Categories(entry).Contains(category, StringComparer.Ordinal))).ToArray();
        if (catalogCount != null) catalogCount.text = HexText.Format("ui_catalog_count", filtered.Length, category.Length == 0 ? Ui("all") : CategoryName(category));
        var rows = new List<KitVirtualRow>();
        for (var offset = 0; offset < filtered.Length; offset += 4)
        {
            var entries = filtered.Skip(offset).Take(4).ToArray(); var key = "catalog:" + entries[0].Identity;
            rows.Add(new KitVirtualRow(key, 108, rect => BindCatalog(rect, key, entries), Unbind));
        }
        keys.Clear(); keys.AddRange(rows.Select(row => row.Key)); list.SetRows(rows, ++revision);
    }
    static void BindCatalog(RectTransform rect, string key, HexSpawnCatalog.Entry[] entries)
    {
        cleanup[rect] = new List<Action>(); var buttons = new List<Button>(); var locks = new List<Image>(); var tips = new List<HexCatalogLiteralTooltip>(); catalogTiles[rect] = buttons;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var button = KitButton.Create(rect, "Tile" + i, "", () => SpawnEditor(entry), new Vector2(i * 177, 0), new Vector2(169, 98));
            buttons.Add(button); locks.Add(LockBadge(button.transform, new Vector2(143, -7)));
            var iconNode = new GameObject("NativeIcon", typeof(RectTransform), typeof(Image)); iconNode.layer = rect.gameObject.layer; iconNode.transform.SetParent(button.transform, false);
            var image = iconNode.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
            image.sprite = entry.Icon != null ? entry.Icon : KitGlyphSprites.Of(KitGlyph.Star);
            KitAlmanacDialog.Place(image.rectTransform, new Vector2(60, -5), new Vector2(48, 48));
            var title = KitAlmanacDialog.Label(button.transform, "NativeName", entry.Name, new Vector2(5, -57), new Vector2(159, 36), 13);
            title.alignment = TextAlignmentOptions.Top; title.overflowMode = TextOverflowModes.Truncate;
            var tip = button.gameObject.AddComponent<HexCatalogLiteralTooltip>(); tip.Topic = entry.Name;
            tip.Detail = entry.Identity + "\n" + string.Join(", ", Categories(entry).Select(CategoryName)) + "\n\n" + HexText.Text("help_spawn_editor"); tips.Add(tip);
        }
        updates[rect] = () =>
        {
            foreach (var badge in locks) badge.gameObject.SetActive(!MeadworksMischief.Decide(Plugin.Guid, MischiefCategory.Economy).Allowed);
            for (var i = 0; i < buttons.Count; i++)
            {
                var decision = KitButton.Block(buttons[i], MeadworksMischief.Query(Plugin.Guid, "Spawn.Create", MischiefCategory.Economy),
                    Plugin.Settings!.Enabled.Value && Plugin.Settings.Spawning.Value && HexMovement.Owned(Player.m_localPlayer));
                tips[i].Literal = decision.Interactable;
                Help(buttons[i].gameObject, tips[i].Topic, tips[i].Detail, decision.Tooltip);
            }
        };
        cleanup[rect].Add(() => catalogTiles.Remove(rect)); updates[rect]();
        // Header occupies two focus rows; retain the same four-column geometry as the visible native icons.
        var at = keys.IndexOf(key); for (var i = 0; i < buttons.Count; i++) window!.Focusable(buttons[i], i + 1, at + 3);
    }
    static void SpawnEditor(HexSpawnCatalog.Entry entry)
    {
        if (!HexSpawn.Allowed || entry.Prefab == null) { Result(HexText.Text("spawn_denied")); return; }
        var dialog = KitAlmanacDialog.Create(window, entry.Name, new Vector2(540, 425)); if (dialog == null) return;
        var amount = 1; var level = 1; var inventory = entry.Kind == HexSpawnKind.Item; var tame = false;
        KitAlmanacDialog.Label(dialog.Panel, "Identity", entry.Identity, new Vector2(20, -50), new Vector2(500, 26), 13);
        KitAlmanacDialog.Label(dialog.Panel, "AmountLabel", Ui("amount"), new Vector2(20, -92), new Vector2(185, 30));
        KitAlmanacDialog.Label(dialog.Panel, "LevelLabel", Ui(entry.Kind == HexSpawnKind.Item ? "quality" : "level"), new Vector2(20, -180), new Vector2(185, 30));
        KitAlmanacNumber Number(string name, Vector2 position, int maximum, Func<int> read, Action<int> write)
        {
            var value = KitAlmanacNumber.Create(dialog.Panel, name, position, new Vector2(300, 75), read().ToString(CultureInfo.InvariantCulture),
                text => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= maximum,
                text => { if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= maximum) write(number); },
                direction => Math.Max(1, Math.Min((long)maximum, (long)read() + direction)).ToString(CultureInfo.InvariantCulture), 1, maximum, 1,
                serialize: number => Math.Round(number).ToString(CultureInfo.InvariantCulture));
            NumberHelp(value, Ui(name == "Amount" ? "amount" : entry.Kind == HexSpawnKind.Item ? "quality" : "level"), HexText.Text(name == "Amount" ? "help_spawn_amount" : "help_spawn_level"));
            dialog.Own(value.Dispose); return value;
        }
        var count = Number("Amount", new Vector2(220, -88), HexSpawnData.MaxAmount, () => amount, value => amount = value);
        var quality = Number("Level", new Vector2(220, -176), entry.MaxQuality, () => level, value => level = value);
        var option = KitToggle.Create(dialog.Panel, "Option", inventory, value => { if (entry.Kind == HexSpawnKind.Item) inventory = value; else tame = value; },
            new Vector2(20, -270), new Vector2(500, 32), Ui(entry.Kind == HexSpawnKind.Item ? "inventory" : "tame"), Ui(entry.Kind == HexSpawnKind.Item ? "drop" : "wild"));
        option.Tooltip(entry.Name, HexText.Text("help_spawn_option"));
        dialog.Own(option.Dispose); option.SetUsable(entry.Kind == HexSpawnKind.Item || entry.Tameable);
        void Submit()
        {
            if (!HexSpawn.Allowed) { Result(HexText.Text("spawn_denied")); return; }
            if (!int.TryParse(count.Field.Text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) || amount < 1 || amount > HexSpawnData.MaxAmount
                || !int.TryParse(quality.Field.Text, NumberStyles.None, CultureInfo.InvariantCulture, out level) || level < 1 || level > entry.MaxQuality) return;
            var request = new HexSpawnRequest(entry.Kind, entry.Identity, amount, level, tame, inventory);
            dialog.Dispose();
            if (amount > Plugin.Settings!.ConfirmSpawnAbove.Value)
                KitAlmanacTextEditor.Confirm(window, Ui("spawn"), HexText.Format("spawn_confirm", amount), () => Result(HexSpawn.Request(request, true)));
            else Result(HexSpawn.Request(request));
        }
        var create = KitButton.Create(dialog.Panel, "Spawn", Ui("spawn"), Submit, new Vector2(280, -350), new Vector2(240, 34));
        var cancel = KitButton.Create(dialog.Panel, "Cancel", Ui("close"), dialog.Dispose, new Vector2(20, -350), new Vector2(240, 34));
        Help(create.gameObject, Ui("spawn"), HexText.Text("help_spawn_submit") + "\n\n" + HexText.Text("spawning")); Help(cancel.gameObject, Ui("close"), HexText.Text("help_close"));
        dialog.Tick += () =>
        {
            if (!HexSpawn.Allowed || entry.Prefab == null) { dialog.Dispose(); return; }
            count.Set(amount.ToString(CultureInfo.InvariantCulture)); quality.Set(level.ToString(CultureInfo.InvariantCulture));
            option.SetUsable(entry.Kind == HexSpawnKind.Item || entry.Tameable); create.interactable = count.Field.Valid && quality.Field.Valid;
        };
        dialog.Window.Focusable(count.Field.Input, 1, 0); dialog.Window.Focusable(count.Less, 0, 0); dialog.Window.Focusable(count.More, 2, 0);
        if (count.Slider != null) dialog.Window.Focusable(count.Slider, 1, 1);
        dialog.Window.Focusable(quality.Field.Input, 1, 2); dialog.Window.Focusable(quality.Less, 0, 2); dialog.Window.Focusable(quality.More, 2, 2);
        if (quality.Slider != null) dialog.Window.Focusable(quality.Slider, 1, 3);
        dialog.Window.Focusable(option.Button, 1, 4); dialog.Window.Focusable(cancel, 0, 5); dialog.Window.Focusable(create, 2, 5);
    }
    static Image LockBadge(Transform parent, Vector2 position)
    {
        var node = new GameObject("AuthorityLock", typeof(RectTransform), typeof(Image)); node.layer = parent.gameObject.layer; node.transform.SetParent(parent, false);
        var image = node.GetComponent<Image>(); image.sprite = KitGlyphSprites.Of(KitGlyph.Lock); image.raycastTarget = false;
        image.color = KitAppearance.Color(KitColorRole.Heading); KitAlmanacDialog.Place(image.rectTransform, position, new Vector2(18, 18)); return image;
    }
    static void PreserveCatalogColumn(GameObject selected, string next)
    {
        var slot = 0;
        foreach (var buttons in catalogTiles.Values) { var at = buttons.FindIndex(button => button != null && button.gameObject == selected); if (at >= 0) { slot = at; break; } }
        list!.Focus(next);
        var first = EventSystem.current?.currentSelectedGameObject;
        foreach (var buttons in catalogTiles.Values)
            if (buttons.Any(button => button != null && button.gameObject == first))
            { var button = buttons[Math.Min(slot, buttons.Count - 1)]; if (button != null && button.IsInteractable()) EventSystem.current?.SetSelectedGameObject(button.gameObject); return; }
    }
    static void ClearCatalog()
    {
        HexCatalogLiteralTooltip.Restore();
        foreach (var control in catalogControls) window?.Unfocusable(control); catalogControls.Clear();
        if (catalogField != null) { catalogField.Changed -= SearchChanged; catalogField.Destroy(); catalogField = null; }
        // KitToggle.Create throws while a control of the same name still exists, and a destroyed control is found until the frame ends: retire it first.
        if (catalogEnable != null) { if (catalogEnable.Button != null) KitUnity.Retire(catalogEnable.Button.gameObject); catalogEnable.Dispose(); catalogEnable = null; }
        if (catalogKind != null) { if (catalogKind.Button != null) KitUnity.Retire(catalogKind.Button.gameObject); catalogKind.Dispose(); catalogKind = null; }
        foreach (var item in catalogObjects) if (item != null) KitUnity.Retire(item); catalogObjects.Clear();
        catalogCount = null; catalog = Array.Empty<HexSpawnCatalog.Entry>(); catalogTiles.Clear(); catalogDirty = false;
    }
}

// Native UITooltip relocalizes raw strings and uses rich text; only these owned catalog markers opt into literal text.
internal sealed class HexCatalogLiteralTooltip : MonoBehaviour
{
    internal string Topic = "", Detail = "";
    internal bool Literal;
    static TMP_Text? topicOwned, detailOwned;
    static bool topicBefore, detailBefore;
    internal static void Restore()
    {
        if (topicOwned != null && !topicOwned.richText) topicOwned.richText = topicBefore;
        if (detailOwned != null && !detailOwned.richText) detailOwned.richText = detailBefore;
        topicOwned = detailOwned = null;
    }
    [HarmonyPatch(typeof(UITooltip), "UpdateTextElements")]
    static class NativeText
    {
        // Native UITooltip reuses one static display. Release our flags before any next owner writes its text.
        static void Prefix() => Restore();
        static void Postfix(UITooltip __instance, GameObject ___m_tooltip)
        {
            var marker = __instance != null ? __instance.GetComponent<HexCatalogLiteralTooltip>() : null;
            if (marker == null || !marker.Literal || ___m_tooltip == null) return;
            var topic = Utils.FindChild(___m_tooltip.transform, "Topic")?.GetComponent<TMP_Text>();
            var detail = Utils.FindChild(___m_tooltip.transform, "Text")?.GetComponent<TMP_Text>();
            if (topic != null) { topicOwned = topic; topicBefore = topic.richText; topic.richText = false; topic.text = marker.Topic; }
            if (detail != null) { detailOwned = detail; detailBefore = detail.richText; detail.richText = false; detail.text = marker.Detail; }
        }
    }
}

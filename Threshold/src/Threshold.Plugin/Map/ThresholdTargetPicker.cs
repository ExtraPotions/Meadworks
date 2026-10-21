// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Picker;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Runtime.Portals;
using ExtraPotions.Threshold.Runtime.Travel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ExtraPotions.Threshold.Runtime.Map
{
    public static class ThresholdTargetPicker
    {
        static KitWindow? window;
        static Button? dropdown, teleport, cancel;
        static GameObject? focusedOption;
        static RectTransform? choicesPanel, content;
        static readonly List<Button> options = new List<Button>();
        static IReadOnlyList<PickerRow> choices = Array.Empty<PickerRow>();
        static PortalId? chosen;
        static long revision;
        static string favourites = "", language = "";
        static float openGap, refreshAt;
        static bool accessModes, exploredOnly;static string prefabFilter = "";
        public static bool IsOpen => window != null && window.IsOpen;
        public static bool Expanded => choicesPanel != null && choicesPanel.gameObject.activeSelf;
        public static RectTransform? Panel => window?.Panel;
        public static bool CanOpen() => Plugin.AllowsUi && CrucibleUiPlugin.Active;

        public static bool Open(TeleportWorld source)
        {
            if (!CanOpen() || ThresholdModules.Mode != TeleportMode.TargetTeleport
                || ThresholdTravel.SourceEntry?.IsTagged != true) return false;
            if (window == null) Build();
            if (window == null) return false;
            chosen = null;
            openGap = Gap(source);
            if (InventoryGui.IsVisible() && InventoryGui.instance != null) InventoryGui.instance.Hide();
            refreshAt = Time.unscaledTime + 5f;
            Refill();window.Open();return true;
        }

        public static void Close() => window?.Close();
        static float Gap(TeleportWorld? source)
        {
            var player = Player.m_localPlayer;
            var trigger = source != null ? source.GetComponentInChildren<TeleportWorldTrigger>() : null;
            var collider = trigger != null ? trigger.GetComponent<Collider>() : null;
            return player == null || collider == null ? float.NaN : Vector3.Distance(collider.ClosestPoint(player.GetCenterPoint()), player.GetCenterPoint());
        }
        static string FavouriteData() => Player.m_localPlayer != null && Player.m_localPlayer.m_customData.TryGetValue(FavouriteSet.CharacterDataKey, out var raw) ? raw : "";
        public static void Tick()
        {
            if (!IsOpen) return;
            var player = Player.m_localPlayer;
            if (!ThresholdTravel.InSession || player == null || player.IsDead() || player.IsTeleporting()
                || !ThresholdModules.PortalRoutingActive() || ThresholdModules.Mode != TeleportMode.TargetTeleport
                || ThresholdTravel.SourceEntry?.IsTagged != true || PortalTriggerRules.Left(openGap, Gap(ThresholdTravel.Source))) { Close();return; }
            if (Expanded) { KeepChoiceVisible();return; }
            if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + 5f;ThresholdPortals.RefreshNow(); }
            var settings = Plugin.Settings;
            if (settings != null && (revision != ThresholdPortals.Revision || favourites != FavouriteData() || language != CrucibleUiPlugin.Language
                || accessModes != settings.Get(ThresholdSettings.Access.AccessModesEnabled) || exploredOnly != settings.Get(ThresholdSettings.General.OnlyExploredPortals)
                || prefabFilter != settings.Get(ThresholdSettings.General.PortalPrefabs))) Refill();
        }

        static void Build()
        {
            window = KitWindow.Create(Plugin.Guid + ".target", new Vector2(480, 340), typing: false);
            if (window == null) return;
            window.Closed += () => { Expand(false);ThresholdTravel.EndSession();chosen = null; };
            var panel = window.Panel;
            var title = KitCanvas.Text(panel, "Title", ThresholdText.Text(ThresholdTextKeys.PickerTitle), 21, Color.white, TextAlignmentOptions.Left, false);
            if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
            Place((RectTransform)title.transform, new Vector2(18, -14), new Vector2(430, 32));
            dropdown = KitButton.Create(panel, "Destination", "", () => Expand(!Expanded), new Vector2(18, -56), new Vector2(444, 34));
            window.Focusable(dropdown, 0, 1);
            var list = new GameObject("Choices", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            choicesPanel = (RectTransform)list.transform;choicesPanel.SetParent(panel, false);Place(choicesPanel, new Vector2(18, -94), new Vector2(444, 184));
            list.GetComponent<Image>().color = new Color(0, 0, 0, .28f);
            content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;content.SetParent(choicesPanel, false);Place(content, Vector2.zero, new Vector2(444, 184));
            var scroll = list.GetComponent<ScrollRect>();scroll.viewport = choicesPanel;scroll.content = content;scroll.horizontal = false;scroll.vertical = true;
            teleport = KitButton.Create(panel, "Teleport", ThresholdText.Text(ThresholdTextKeys.PickerTeleport), Go, new Vector2(254, -292), new Vector2(98, 32));
            cancel = KitButton.Create(panel, "Cancel", ThresholdText.Text(ThresholdTextKeys.PickerCancel), Close, new Vector2(360, -292), new Vector2(102, 32));
            window.Focusable(teleport, 0, 20);window.Focusable(cancel, 1, 20);Expand(false);
        }
        static void Expand(bool open)
        {
            if (choicesPanel == null) return;
            choicesPanel.gameObject.SetActive(open);
            if (window == null) return;
            focusedOption = null;
            for (int i = 0; i < options.Count; i++) if (open) window.Focusable(options[i], 0, i + 2);else window.Unfocusable(options[i]);
            if (teleport != null) { window.Unfocusable(teleport);window.Focusable(teleport, 0, open ? options.Count + 3 : 2); }
            if (cancel != null) { window.Unfocusable(cancel);window.Focusable(cancel, 1, open ? options.Count + 3 : 2); }
        }
        static void KeepChoiceVisible()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == focusedOption) return;
            focusedOption = selected;
            int index = options.FindIndex(button => button != null && button.gameObject == selected);
            if (index < 0 || content == null || choicesPanel == null) return;
            float offset = content.anchoredPosition.y, top = index * 34f, bottom = top + 34f;
            if (top < offset) offset = top;else if (bottom > offset + choicesPanel.rect.height) offset = bottom - choicesPanel.rect.height;
            var at = content.anchoredPosition;at.y = Mathf.Clamp(offset, 0, Mathf.Max(0, content.sizeDelta.y - choicesPanel.rect.height));content.anchoredPosition = at;
        }
        static void Refill()
        {
            var player = Player.m_localPlayer;var settings = Plugin.Settings;
            if (player == null || settings == null || content == null) return;
            favourites = FavouriteData();language = CrucibleUiPlugin.Language;
            accessModes = settings.Get(ThresholdSettings.Access.AccessModesEnabled);exploredOnly = settings.Get(ThresholdSettings.General.OnlyExploredPortals);prefabFilter = settings.Get(ThresholdSettings.General.PortalPrefabs);
            var at = player.transform.position;
            var query = new PickerQuery(ThresholdPortals.Current, ThresholdTravel.SourceEntry, new MapPoint(at.x, at.y, at.z),
                new AccessViewer(player.GetPlayerID(), ZNet.instance != null && ZNet.instance.LocalPlayerIsAdminOrHost()),
                settings.Get(ThresholdSettings.Access.AccessModesEnabled), "", SortOrder.Name, FavouriteSet.Parse(favourites), false,
                settings.Get(ThresholdSettings.General.OnlyExploredPortals) ? (Func<MapPoint, bool>)ThresholdMapPane.Explored : null,
                PrefabFilter.Parse(settings.Get(ThresholdSettings.General.PortalPrefabs)));
            choices = PortalModeRules.NamedChoices(query);revision = ThresholdPortals.Revision;
            foreach (var old in options) if (old != null) { window?.Unfocusable(old);old.gameObject.SetActive(false);old.name = "RetiredChoice";old.transform.SetParent(null, false);UnityEngine.Object.Destroy(old.gameObject); }
            options.Clear();
            if (!chosen.HasValue || !choices.Any(row => row.Portal.Id.Equals(chosen.Value))) chosen = choices.Count > 0 ? choices[0].Portal.Id : (PortalId?)null;
            for (int i = 0; i < choices.Count; i++)
            {
                var row = choices[i];
                var button = KitButton.Create(content, "Choice_" + i, Label(row), () => { chosen = row.Portal.Id;Caption();Expand(false); }, new Vector2(0, -i * 34), new Vector2(444, 32));
                options.Add(button);
            }
            content.sizeDelta = new Vector2(444, Math.Max(184, choices.Count * 34));Caption();Expand(false);
            var title = window?.Panel.Find("Title")?.GetComponent<TMP_Text>();if (title != null) title.text = ThresholdText.Text(ThresholdTextKeys.PickerTitle);
            if (teleport != null) SetText(teleport, ThresholdText.Text(ThresholdTextKeys.PickerTeleport));
            if (cancel != null) SetText(cancel, ThresholdText.Text(ThresholdTextKeys.PickerCancel));
        }
        static string Label(PickerRow row) => row.Portal.Tag.Trim().RemoveRichTextTags() + "  " + ThresholdText.Format(ThresholdTextKeys.PickerDistance, Math.Round(row.Distance).ToString("0", CultureInfo.InvariantCulture));
        static void Caption()
        {
            var row = chosen.HasValue ? choices.FirstOrDefault(value => value.Portal.Id.Equals(chosen.Value)) : null;
            if (dropdown != null) { SetText(dropdown, row == null ? ThresholdText.Text(ThresholdTextKeys.PickerEmpty) : Label(row) + "  v");dropdown.interactable = choices.Count > 0; }
            if (teleport != null) teleport.interactable = row != null;
        }
        public static void Go()
        {
            if (!IsOpen || !chosen.HasValue || ThresholdModules.Mode != TeleportMode.TargetTeleport) return;
            var target = choices.FirstOrDefault(row => row.Portal.Id.Equals(chosen.Value));
            if (target == null) return;
            var current = ThresholdPortals.Current.FirstOrDefault(portal => portal.Id.Equals(target.Portal.Id));
            if (current != null && current.IsTagged) ThresholdTravel.TravelTo(current);
        }
        static void SetText(Button button, string text) { var label = button.GetComponentInChildren<TMP_Text>();if (label != null) label.text = text; }
        static void Place(RectTransform rect, Vector2 at, Vector2 size) { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);rect.anchoredPosition = at;rect.sizeDelta = size; }
        public static void Shutdown() { if (window != null) { window.Destroy();window = null; }options.Clear();choices = Array.Empty<PickerRow>();dropdown = teleport = cancel = null;choicesPanel = content = null;chosen = null; }
    }
}

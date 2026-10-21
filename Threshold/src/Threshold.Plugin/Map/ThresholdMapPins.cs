// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: portal pins and name tags on the game's ordinary large map (parity #13), colored by access mode
// (#72) and spread when they stand close together (#70). Behavior as Wayfare's map icon layer (GPL-3.0-or-later, the
// reference: Threshold's own layer over the large map, kept last so it draws over the game's pins, never the game's pin
// list, which would save the pins and cross them out on a click) and PotalMap's portal pins (behaviour only). Show Portal
// Pins is an ordinary setting (F8 and Almanac); Portal Pins Key (unbound by default; on the open large map, as PotalMap's
// icon toggle) turns it on and off.
using System;
using System.Collections.Generic;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Threshold.Access;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Map;
using ExtraPotions.Threshold.Picker;
using ExtraPotions.Threshold.Portals;
using ExtraPotions.Threshold.Runtime.Portals;
using ExtraPotions.Threshold.Runtime.Travel;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Threshold.Runtime.Map
{
    public static class ThresholdMapPins
    {
        public const string LayerName = "Threshold.MapPins";
        const string PortalPrefab = "portal_wood";
        const float ListSeconds = 0.5f;
        const float LabelWidth = 160f;

        delegate void WorldToMap(Minimap minimap, Vector3 point, out float mx, out float my);
        static readonly WorldToMap WorldToMapPoint = AccessTools.MethodDelegate<WorldToMap>(
            AccessTools.Method(typeof(Minimap), "WorldToMapPoint", new[] { typeof(Vector3), typeof(float).MakeByRefType(), typeof(float).MakeByRefType() }));

        sealed class Icon
        {
            public RectTransform Root = null!;
            public Image Image = null!;
            public TMP_Text Label = null!;
            public Button Button = null!;
            public ThresholdMapPinClick Click = null!;
            public PortalEntry? Target;
        }

        static RectTransform? layer;
        static readonly List<Icon> icons = new List<Icon>();
        static IReadOnlyList<MapPinSpec> pins = Array.Empty<MapPinSpec>();
        static float listedAt = float.NegativeInfinity;
        static bool listedForTravel;
        static readonly List<PinPoint> points = new List<PinPoint>();
        static readonly List<PinPoint> spread = new List<PinPoint>();
        static readonly List<LabelSide> sides = new List<LabelSide>();
        static readonly PinLayoutCache layoutCache = new PinLayoutCache();
        static readonly List<int> placed = new List<int>();
        static Sprite? glyph;
        static RectTransform? here;
        static Image? hereImage;
        static TMP_Text? hereLabel;

        /// <summary>The pins are on the open large map now (the server is asked for the list every 5 s meanwhile).</summary>
        public static bool Showing { get; private set; }

        /// <summary>Pins drawn this frame (inside the map's view).</summary>
        public static int Drawn { get; private set; }

        /// <summary>Portal Pins Key: turns Show Portal Pins off (on) and tells the player.</summary>
        public static void Toggle()
        {
            var settings = Plugin.Settings;
            if (settings == null) return;
            bool next = !settings.Get(ThresholdSettings.Map.ShowPortalPins);
            var result = MeadworksConfig.Amend(settings.Entry(ThresholdSettings.Map.ShowPortalPins), next, "Threshold menu");
            if (!result.Applied) return;
            Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft,
                ThresholdText.Text(next ? ThresholdTextKeys.MenuPortalPinsOn : ThresholdTextKeys.MenuPortalPinsOff));
        }

        /// <summary>From the plugin's LateUpdate, after the game moved its map this frame.</summary>
        public static void LateTick()
        {
            var minimap = Minimap.instance;
            var settings = Plugin.Settings;
            if (KeyToggles(minimap, settings)) Toggle();
            bool show = minimap != null && settings != null && Player.m_localPlayer != null && WorldToMapPoint != null
                && OrdinaryMapPins.Shows(Plugin.AllowsUi, ThresholdModules.PortalRoutingActive(), settings.Get(ThresholdSettings.Map.ShowPortalPins),
                    minimap.m_mode == Minimap.MapMode.Large, ThresholdTravel.InSession);
            if (!show)
            {
                Hide();
                return;
            }
            try { Draw(minimap!, settings!); }
            catch (Exception e)
            {
                Plugin.Log?.Error("Threshold: map pins failed; they stay off until the map opens again: " + e);
                Hide();
            }
        }

        /// <summary>Portal Pins Key went down on the open large map, outside the travel picker (its pins always show) and
        /// never while typing (the map's pin name field, chat, console).</summary>
        static bool KeyToggles(Minimap? minimap, ThresholdConfig? settings)
        {
            if (minimap == null || settings == null || minimap.m_mode != Minimap.MapMode.Large || ThresholdPickerWindow.IsOpen
                || !ThresholdModules.PortalRoutingActive() || Minimap.InTextInput()) return false;
            return MeadworksInput.Pressed(settings.Shortcut(ThresholdSettings.Map.PortalPinsKey), InputContext.Window);
        }

        static void Draw(Minimap minimap, ThresholdConfig settings)
        {
            if (!Showing)
            {
                Showing = true;
                listedAt = float.NegativeInfinity;
                ThresholdPortals.RefreshNow();
            }
            if (listedForTravel != ThresholdPickerWindow.IsOpen) listedAt = float.NegativeInfinity;
            if (Time.unscaledTime - listedAt >= ListSeconds)
            {
                listedAt = Time.unscaledTime;
                listedForTravel = ThresholdPickerWindow.IsOpen;
                pins = ThresholdPickerWindow.IsOpen ? ThresholdPickerWindow.SessionPins : OrdinaryMapPins.Build(PickerList.Build(Query(settings)),
                    settings.Get(ThresholdSettings.Access.AccessModesEnabled), settings.Get(ThresholdSettings.Picker.HighlightStonePortals));
            }
            var root = Layer(minimap);
            if (root == null) return;
            var image = minimap.m_mapImageLarge;
            var rect = image.rectTransform.rect;
            var view = new MapView(image.uvRect.xMin, image.uvRect.yMin, image.uvRect.width, image.uvRect.height, rect.width, rect.height);
            bool labels = settings.Get(ThresholdSettings.Map.ShowMapLabels);
            var untagged = ThresholdText.Text(ThresholdTextKeys.PickerUntagged);

            points.Clear();
            placed.Clear();
            for (int i = 0; i < pins.Count; i++)
            {
                var at = pins[i].Portal!.Position;
                WorldToMapPoint(minimap, new Vector3(at.X, at.Y, at.Z), out float mx, out float my);
                if (!OrdinaryMapPins.Visible(mx, my, view)) continue;
                points.Add(OrdinaryMapPins.ToGui(mx, my, view));
                placed.Add(i);
            }
            bool spreadOn = settings.Get(ThresholdSettings.Map.SpreadClusteredPins);
            if (!layoutCache.Matches(view, spreadOn, points))
            {
                PinSpread.LayoutInto(points, OrdinaryMapPins.IconSize, spreadOn, spread, sides);
                layoutCache.Remember(view, spreadOn, points);
            }
            var sprite = PortalIcon(minimap);
            for (int k = 0; k < placed.Count; k++)
            {
                var pin = pins[placed[k]];
                var icon = Take(root, k);
                icon.Target = pin.Portal;
                icon.Click.Target = pin.Portal;
                icon.Button.interactable = ThresholdPickerWindow.IsOpen;
                icon.Image.raycastTarget = ThresholdPickerWindow.IsOpen;
                icon.Root.name = Name(pin.Portal!.Id);
                icon.Root.anchoredPosition = new Vector2(spread[k].X, spread[k].Y);
                if (icon.Image.sprite != sprite) icon.Image.sprite = sprite;
                var colour = MapPinColours.Tint(pin);
                var tint = new Color(colour.R, colour.G, colour.B, colour.A);
                icon.Image.color = tint;
                // The tag in its pin's color, as PotalMap's labels.
                icon.Label.color = tint;
                var label = OrdinaryMapPins.Label(pin, labels, untagged);
                if (icon.Label.text != label) icon.Label.text = label;
                Place(icon.Label, sides[k]);
                if (!icon.Root.gameObject.activeSelf) icon.Root.gameObject.SetActive(true);
            }
            for (int k = placed.Count; k < icons.Count; k++)
                if (icons[k].Root != null && icons[k].Root.gameObject.activeSelf) icons[k].Root.gameObject.SetActive(false);
            Drawn = placed.Count;
            DrawHere(minimap, root, view, sprite);
            var browser = ThresholdPickerWindow.Panel;
            if (ThresholdPickerWindow.IsOpen && browser != null) browser.SetAsLastSibling();
        }

        /// <summary>The you-are-here marker (4C-11): the portal walked into, faded in its color, while the picker is open;
        /// else the words alone over the game's own player arrow. Never clickable.</summary>
        static void DrawHere(Minimap minimap, RectTransform root, MapView view, Sprite? sprite)
        {
            var player = Player.m_localPlayer;
            bool picking = ThresholdPickerWindow.IsOpen;
            var portal = picking ? ThresholdPickerWindow.HerePortal : null;
            MapPoint? playerAt = null;
            if (player != null) { var p = player.transform.position; playerAt = new MapPoint(p.x, p.y, p.z); }
            var at = PickerMapClicks.HereAt(picking, portal, playerAt);
            float mx = 0f, my = 0f;
            if (at != null) WorldToMapPoint(minimap, new Vector3(at.Value.X, at.Value.Y, at.Value.Z), out mx, out my);
            if (at == null || !OrdinaryMapPins.Visible(mx, my, view))
            {
                if (here != null && here.gameObject.activeSelf) here.gameObject.SetActive(false);
                return;
            }
            var marker = Here(root);
            var gui = OrdinaryMapPins.ToGui(mx, my, view);
            marker.anchoredPosition = new Vector2(gui.X, gui.Y);
            bool icon = portal != null && sprite != null;
            if (hereImage!.enabled != icon) hereImage.enabled = icon;
            if (icon)
            {
                if (hereImage.sprite != sprite) hereImage.sprite = sprite;
                var colour = MapPinColours.ForPin(ThresholdPickerWindow.HerePin!, false);
                hereImage.color = new Color(colour.R, colour.G, colour.B, colour.A);
            }
            var words = ThresholdText.Text(ThresholdTextKeys.MapYouAreHere);
            if (hereLabel!.text != words) hereLabel.text = words;
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            if (marker.GetSiblingIndex() != root.childCount - 1) marker.SetAsLastSibling();
        }

        static RectTransform Here(RectTransform root)
        {
            if (here != null && here.parent == root) return here;
            var go = new GameObject("YouAreHere", typeof(RectTransform), typeof(Image));
            here = (RectTransform)go.transform;
            here.SetParent(root, false);
            here.anchorMin = here.anchorMax = Vector2.zero;
            here.pivot = new Vector2(0.5f, 0.5f);
            here.sizeDelta = new Vector2(OrdinaryMapPins.IconSize, OrdinaryMapPins.IconSize);
            hereImage = go.GetComponent<Image>();
            hereImage.raycastTarget = false;
            hereImage.preserveAspect = true;
            hereLabel = KitCanvas.Text(here, "Label", "", 14f, KitUnity.Colour(KitPalette.TabLitText), TextAlignmentOptions.Bottom, false);
            hereLabel.raycastTarget = false;
            if (KitCanvas.Heading != null) hereLabel.font = KitCanvas.Heading;
            var rect = (RectTransform)hereLabel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(LabelWidth, 20f);
            return here;
        }

        /// <summary>The list query of the picker with no current portal: every portal this player may use.</summary>
        static PickerQuery Query(ThresholdConfig settings)
        {
            var player = Player.m_localPlayer!;
            var at = player.transform.position;
            var viewer = new AccessViewer(player.GetPlayerID(), ZNet.instance != null && ZNet.instance.LocalPlayerIsAdminOrHost());
            return new PickerQuery(ThresholdPortals.Current, null, new MapPoint(at.x, at.y, at.z), viewer,
                settings.Get(ThresholdSettings.Access.AccessModesEnabled), "", settings.Get(ThresholdSettings.Picker.Sort), FavouriteSet.Parse(
                    player.m_customData.TryGetValue(FavouriteSet.CharacterDataKey, out var raw) ? raw : null),
                settings.Get(ThresholdSettings.General.ListUntaggedPortals),
                settings.Get(ThresholdSettings.General.OnlyExploredPortals) ? (Func<MapPoint, bool>)ThresholdMapPane.Explored : null,
                PrefabFilter.Parse(settings.Get(ThresholdSettings.General.PortalPrefabs)), settings.Get(ThresholdSettings.Picker.HighlightStonePortals));
        }

        /// <summary>Object name of a portal's pin, so a pin can be told apart in the scene.</summary>
        public static string Name(PortalId id) => "Pin_" + id.User + "_" + id.Id;

        /// <summary>Threshold's layer over the large map: stretched over the map, the last child so it draws over the
        /// game's pins and markers; null while there is no large map.</summary>
        static RectTransform? Layer(Minimap minimap)
        {
            var parent = minimap.m_pinRootLarge != null ? minimap.m_pinRootLarge.parent as RectTransform : null;
            if (parent == null) return null;
            if (layer == null || layer.parent != parent)
            {
                Destroy();
                var go = new GameObject(LayerName, typeof(RectTransform));
                layer = (RectTransform)go.transform;
                layer.SetParent(parent, false);
                layer.anchorMin = Vector2.zero;
                layer.anchorMax = Vector2.one;
                layer.offsetMin = Vector2.zero;
                layer.offsetMax = Vector2.zero;
            }
            if (layer.GetSiblingIndex() != parent.childCount - 1) layer.SetAsLastSibling();
            if (!layer.gameObject.activeSelf) layer.gameObject.SetActive(true);
            return layer;
        }

        static Icon Take(RectTransform root, int index)
        {
            while (icons.Count <= index)
            {
                var go = new GameObject("Pin", typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = (RectTransform)go.transform;
                rect.SetParent(root, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(OrdinaryMapPins.IconSize, OrdinaryMapPins.IconSize);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.preserveAspect = true;
                var label = KitCanvas.Text(rect, "Label", "", 14f, Color.white, TextAlignmentOptions.Center, false);
                label.raycastTarget = false;
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
                labelRect.pivot = new Vector2(0.5f, 1f);
                labelRect.anchoredPosition = new Vector2(0f, -2f);
                labelRect.sizeDelta = new Vector2(LabelWidth, 20f);
                var icon = new Icon { Root = rect, Image = image, Label = label, Button = go.GetComponent<Button>(), Click = go.AddComponent<ThresholdMapPinClick>() };
                icon.Button.targetGraphic = image;
                // Clicks go through ThresholdMapPinClick: travel after the game's double-click window, a double click
                // places a map pin, a right click removes one (4C-11).
                icons.Add(icon);
            }
            return icons[index];
        }

        /// <summary>The game's own map portal pin glyph (the pin type players mark portals with), as Wayfare's icons; the
        /// portal piece's icon only if the game has none.</summary>
        static Sprite? PortalIcon(Minimap minimap)
        {
            if (glyph != null) return glyph;
            foreach (var data in minimap.m_icons)
                if (data.m_name == Minimap.PinType.Icon4 && data.m_icon != null)
                    return glyph = data.m_icon;
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(PortalPrefab) : null;
            return prefab != null ? prefab.GetComponent<Piece>()?.m_icon : null;
        }

        /// <summary>A tag below its pin as the game's pins, or beside it on the side away from a spread cluster's center.</summary>
        static void Place(TMP_Text label, LabelSide side)
        {
            var rect = (RectTransform)label.transform;
            Vector2 anchor, pivot, offset;
            TextAlignmentOptions align;
            switch (side)
            {
                case LabelSide.Above: anchor = new Vector2(0.5f, 1f); pivot = new Vector2(0.5f, 0f); offset = new Vector2(0f, 2f); align = TextAlignmentOptions.Bottom; break;
                case LabelSide.Left: anchor = new Vector2(0f, 0.5f); pivot = new Vector2(1f, 0.5f); offset = new Vector2(-2f, 0f); align = TextAlignmentOptions.Right; break;
                case LabelSide.Right: anchor = new Vector2(1f, 0.5f); pivot = new Vector2(0f, 0.5f); offset = new Vector2(2f, 0f); align = TextAlignmentOptions.Left; break;
                default: anchor = new Vector2(0.5f, 0f); pivot = new Vector2(0.5f, 1f); offset = new Vector2(0f, -2f); align = TextAlignmentOptions.Top; break;
            }
            if (rect.anchorMin != anchor) rect.anchorMin = rect.anchorMax = anchor;
            if (rect.pivot != pivot) rect.pivot = pivot;
            if (rect.anchoredPosition != offset) rect.anchoredPosition = offset;
            if (label.alignment != align) label.alignment = align;
        }

        /// <summary>The map closed, the setting is off or Threshold stopped: the pins are hidden once.</summary>
        static void Hide()
        {
            Drawn = 0;
            if (!Showing) return;
            Showing = false;
            if (layer != null && layer.gameObject.activeSelf) layer.gameObject.SetActive(false);
        }

        static void Destroy()
        {
            if (layer != null) UnityEngine.Object.Destroy(layer.gameObject);
            layer = null;
            icons.Clear();
            here = null;
            hereImage = null;
            hereLabel = null;
        }

        public static void Shutdown()
        {
            Showing = false;
            Drawn = 0;
            pins = Array.Empty<MapPinSpec>();
            glyph = null;
            Destroy();
        }
    }
}

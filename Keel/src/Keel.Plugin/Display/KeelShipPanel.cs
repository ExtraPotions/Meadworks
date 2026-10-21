// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the ship panel on the Crucible.UI kit (K09, design 10 and 4.5) and the boat camera distance (C13).
using System;
using System.Globalization;
using System.Linq;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Display;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Keel.Performance;
using ExtraPotions.Keel.Sailing;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// A compact HUD readout below the steering indicator while the local player is aboard, laid out as Solo's ship panel
    /// (ShipConfig): a dark translucent box with gray labels on the left and white values on the right. Rows: speed in the
    /// player's unit (km/h by default), its top speed against the vanilla ship of its hull, the exploration radius (Cairn's value through
    /// <c>meadworks.explore.current</c> when present), and a kit meter each for Wind Call and Lookout (locked, recharging
    /// or ready; hidden when the ability is off). Display-only; hidden in menus, the inventory and the map. Main thread,
    /// graphical clients only.
    /// </summary>
    internal static class KeelShipPanel
    {
        private const int Width = 174;
        private const int LineHeight = 14;
        private const int TitleHeight = 0;
        private const int Lines = 3;
        private const int MeterHeight = 14;
        private const int Meters = 2;
        private const int Height = TitleHeight + Lines * LineHeight + Meters * (MeterHeight + 4) + 10;
        private const int FallbackSteeringBottom = 410;
        private const int Side = 7;
        private const int ValueWidth = 56;
        private static readonly Color BoxColour = new Color(0f, 0f, 0f, 0.4f);
        private static readonly Color LabelColour = new Color(0.72f, 0.72f, 0.72f, 1f);

        private static KitHudPanel? panel;
        private static TMP_Text? title;
        private static readonly TMP_Text?[] lines = new TMP_Text?[Lines];
        private static readonly TMP_Text?[] values = new TMP_Text?[Lines];
        private static readonly KitMeter?[] meters = new KitMeter?[Meters];
        private static Transform? hudParent;
        private static (int X, int Y) placed = (-1, -1);

        // The numbers and meters change a few times a second at most; place and fill them ten times a second, and write a
        // text or meter only when it differs from what the label shows (a TMP text rebuilds its mesh and dirties the HUD canvas).
        private const double RefreshSeconds = 0.1;
        private static readonly RefreshGate refresh = new RefreshGate(RefreshSeconds);
        private static readonly ChangeGate<string> titleShown = new ChangeGate<string>();
        private static readonly ChangeGate<string>[] lineShown = { new ChangeGate<string>(), new ChangeGate<string>(), new ChangeGate<string>() };
        private static readonly ChangeGate<(bool, float, string)>[] meterShown = { new ChangeGate<(bool, float, string)>(), new ChangeGate<(bool, float, string)>() };
        private static readonly Vector3[] corners = new Vector3[4];

        public static void Update()
        {
            var settings = Plugin.Settings;
            var root = Hud.instance != null ? Hud.instance.m_rootObject : null;
            var player = Player.m_localPlayer;
            var ship = player != null ? Ship.GetLocalShip() : null;
            if (settings == null || !PanelModel.Visible(settings.Get(KeelSettings.Display.ShipPanel), Plugin.AllowsUi, KeelModules.ProductActive(out _),
                    ship != null && !player!.IsDead(), () => root == null || !root.activeInHierarchy
                        || InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || Console.IsVisible() || Game.IsPaused()))
            {
                Hide();
                return;
            }
            if (panel == null || panel.Root == null || hudParent != root!.transform)
            {
                Dispose();
                if (!Create(root!.transform)) return;
            }
            if (!refresh.Due(Time.unscaledTime)) return;
            Place();
            Fill(settings, ship!);
            panel!.Update(true, false, 1f, 0f);
        }

        /// <summary>The panel was recreated, hidden or disposed: the next Update fills it at once and rewrites every text.</summary>
        private static void ResetGates()
        {
            refresh.Reset();
            titleShown.Reset();
            foreach (var gate in lineShown) gate.Reset();
            foreach (var gate in meterShown) gate.Reset();
        }

        public static void Dispose()
        {
            panel?.Dispose();
            panel = null;
            title = null;
            for (var i = 0; i < lines.Length; i++) lines[i] = values[i] = null;
            for (var i = 0; i < meters.Length; i++)
            {
                meters[i]?.Dispose();
                meters[i] = null;
            }
            hudParent = null;
            placed = (-1, -1);
            ResetGates();
        }

        private static void Hide()
        {
            if (panel?.Root != null) panel.Root.gameObject.SetActive(false);
            ResetGates();
        }

        private static bool Create(Transform parent)
        {
            hudParent = parent;
            ResetGates();
            panel = KitHudPanel.Create(parent, "Keel_ShipPanel", new Vector2(Width, Height), Vector2.zero, global::ExtraPotions.Keel.Runtime.Plugin.Guid, "Keel_ShipPanel");
            if (panel == null) return false;
            title = KitCanvas.Text(panel.Content, "Title", "", 14, Color.white, TextAlignmentOptions.Left, false);
            Put(title.rectTransform, 8, 4, Width - 16, TitleHeight);
            title.gameObject.SetActive(false);
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            box.rectTransform.SetParent(panel.Content, false);
            box.rectTransform.SetAsFirstSibling();
            box.rectTransform.anchorMin = Vector2.zero;
            box.rectTransform.anchorMax = Vector2.one;
            box.rectTransform.offsetMin = box.rectTransform.offsetMax = Vector2.zero;
            box.color = BoxColour;
            box.raycastTarget = false;
            for (var i = 0; i < Lines; i++)
            {
                var y = 4 + TitleHeight + i * LineHeight;
                lines[i] = KitCanvas.Fit(KitCanvas.Text(panel.Content, "Line" + i, "", 12, LabelColour, TextAlignmentOptions.Left, false));
                Put(lines[i]!.rectTransform, Side, y, Width - 2 * Side - ValueWidth, LineHeight);
                values[i] = KitCanvas.Fit(KitCanvas.Text(panel.Content, "Value" + i, "", 12, Color.white, TextAlignmentOptions.Right, false));
                Put(values[i]!.rectTransform, Width - Side - ValueWidth, y, ValueWidth, LineHeight);
            }
            for (var i = 0; i < Meters; i++)
            {
                meters[i] = KitMeter.Create(panel.Content, i == 0 ? "WindCall" : "Lookout",
                    new Vector2(8, -(4 + TitleHeight + Lines * LineHeight + i * (MeterHeight + 4))), new Vector2(Width - 16, MeterHeight));
                if (meters[i] != null) meters[i]!.Label.fontSize = 11;
            }
            return true;
        }

        private static void Put(RectTransform rect, int x, int y, int width, int height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Place()
        {
            if (!(panel!.Root.parent is RectTransform parent)) return;
            var size = parent.rect.size;
            var at = SteeringHome(parent);
            if (at == placed) return;
            placed = at;
            panel.SetPosition(new Vector2(at.X, at.Y));
        }

        /// <summary>Aligns the readout with the steering indicator's right edge and places it below the circle.</summary>
        private static (int X, int Y) SteeringHome(RectTransform parent)
        {
            var wind = Hud.instance != null && Hud.instance.m_shipHudRoot != null && Hud.instance.m_shipHudRoot.activeInHierarchy ? Hud.instance.m_shipWindIndicatorRoot : null;
            if (wind == null || !wind.gameObject.activeInHierarchy) return PanelModel.Home((int)parent.rect.width, Width, FallbackSteeringBottom);
            wind.GetWorldCorners(corners);
            var edge = parent.InverseTransformPoint(corners[3]);
            // Below the forged dial when it shows: it reaches past the steering circle.
            var dial = KeelDial.Shown;
            if (dial != null)
            {
                dial.GetWorldCorners(corners);
                edge.y = Mathf.Min(edge.y, parent.InverseTransformPoint(corners[3]).y);
            }
            return (Mathf.RoundToInt(edge.x - parent.rect.xMin - Width), Mathf.RoundToInt(parent.rect.yMax - edge.y) + PanelModel.Gap);
        }

        private static void Fill(KeelConfig settings, Ship ship)
        {
            var name = KeelCatalog.ShipName(ship);
            if (name.Length == 0)
            {
                var piece = ship.GetComponent<Piece>();
                name = piece != null && global::Localization.instance != null ? global::Localization.instance.Localize(piece.m_name) : "";
            }
            if (titleShown.Changed(name)) title!.text = name;
            var unit = settings.Get(KeelSettings.Display.SpeedUnit);
            var vanilla = KeelCatalog.TryVanillaSailForce(Utils.GetPrefabName(ship.gameObject), out var v) ? v : 0f;
            var factor = PanelModel.SpeedFactor(ship.m_sailForceFactor, vanilla, HelmFactor(settings, ship));
            var shown = PanelModel.Lines(ship.GetSpeed(), unit, factor, KeelExplore.PanelRadius(), CultureInfo.CurrentCulture).ToList();
            for (var i = 0; i < Lines && i < shown.Count; i++)
            {
                var line = shown[i];
                var label = KeelText.Text(line.Key);
                var value = line.UnitKey.Length == 0 ? line.Number : line.Number + " " + KeelText.Text(line.UnitKey);
                if (lineShown[i].Changed(label + "|" + value)) { lines[i]!.text = label; values[i]!.text = value; }
            }
            // Visible meters stack under the rows and the box ends below the last one, as Solo's panel fits its rows.
            var stacked = 0;
            for (var i = 0; i < Meters; i++)
            {
                Meter(meters[i], i == 0, meterShown[i]);
                if (meters[i]?.Root == null || !meters[i]!.Root.gameObject.activeSelf) continue;
                meters[i]!.Root.anchoredPosition = new Vector2(8, -(4 + TitleHeight + Lines * LineHeight + stacked * (MeterHeight + 4)));
                stacked++;
            }
            panel!.Root.sizeDelta = new Vector2(Width, TitleHeight + Lines * LineHeight + stacked * (MeterHeight + 4) + 10);
        }

        /// <summary>One ability's kit meter: hidden when the ability is off, else its fill and status text.</summary>
        private static void Meter(KitMeter? meter, bool windCall, ChangeGate<(bool, float, string)> shown)
        {
            if (meter == null || meter.Root == null) return;
            var state = KeelActives.Meter(windCall);
            var visible = state.Visible && state.Key != KeelTextKeys.AbilityNeedsLevel;
            meter.Root.gameObject.SetActive(visible);
            if (!visible)
            {
                shown.Reset();
                return;
            }
            var name = KeelText.Text(windCall ? KeelTextKeys.AbilityWindCall : KeelTextKeys.AbilityLookout);
            var text = state.Key == KeelTextKeys.AbilityReady ? name + ": " + KeelText.Text(state.Key) : KeelText.Format(state.Key, name, state.Argument);
            if (shown.Changed((true, state.Fill, text))) meter.Set(state.Fill, 1f, text);
        }

        private static float HelmFactor(KeelConfig settings, Ship ship)
        {
            if (!SailingPerks.Active) return 1f;
            var helmsman = SailingPerks.Helmsman(ship);
            return SailingRules.HelmSpeedFactor(true, helmsman != null, settings.Get(KeelSettings.Sailing.ShipSpeedAt100), KeelSailing.Level(helmsman));
        }
    }

    /// <summary>The boat camera's farthest distance while steering (Local setting); the game's own value whenever Keel does not act.</summary>
    internal static class KeelBoatCamera
    {
        private static GameCamera? camera;
        private static float original;

        public static void Update()
        {
            var current = GameCamera.instance;
            if (current == null) return;
            if (current != camera)
            {
                camera = current;
                original = current.m_maxDistanceBoat;
            }
            var settings = Plugin.Settings;
            var active = settings != null && Plugin.AllowsUi && KeelModules.Active(KeelModule.Catalog);
            var value = PanelModel.BoatCamera(active, settings != null ? settings.Get(KeelSettings.Display.BoatCameraMaxDistance) : original, original);
            if (!Mathf.Approximately(current.m_maxDistanceBoat, value)) current.m_maxDistanceBoat = value;
        }

        public static void Restore()
        {
            if (camera != null) camera.m_maxDistanceBoat = original;
            camera = null;
        }
    }
}

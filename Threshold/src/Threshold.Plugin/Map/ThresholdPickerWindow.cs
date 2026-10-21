// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Native large-map travel host with a left-hand destination browser.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtraPotions.Threshold.Runtime.Map
{
    public sealed class ThresholdDestinationContext : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler
    {
        public int Line;
        public void OnPointerEnter(PointerEventData data) => ThresholdPickerWindow.Focus(Line);
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Right) ThresholdPickerWindow.Context(Line);
        }
    }

    public static class ThresholdPickerWindow
    {
        const float RowHeight = 34f;
        const float ListWidth = 300f;
        static readonly Vector2 Size = new Vector2(300f, 580f);

        static NativeMapHost? window;
        public static bool IsOpen => window != null && window.IsOpen;
        public static RectTransform? Panel => window?.Panel;
        /// <summary>The portal walked into (the picker's Here pin), or null.</summary>
        public static PortalEntry? HerePortal => HerePin?.Portal;
        public static MapPinSpec? HerePin => view != null && view.Plan.HereIndex >= 0 ? view.Plan.Pins[view.Plan.HereIndex] : null;
        public static IReadOnlyList<MapPinSpec> SessionPins => view == null ? Array.Empty<MapPinSpec>() : view.Plan.Pins.Take(view.Plan.DestinationCount).ToArray();
        static KitTextField? search;
        static RectTransform? content;
        static RectTransform? viewport;
        static GameObject? lastSelected;
        static TMP_Text? emptyText;
        static Button? closeButton, favourite, home, sourceHub, returnHub;
        static readonly List<Button> rows = new List<Button>();
        static readonly List<Button> padControls = new List<Button>();
        static readonly List<int> padStamp = new List<int>();
        static readonly List<int> padSeen = new List<int>();
        static int padGeneration;
        static bool padReady;
        static PickerView? view;
        static int selected = -1;
        static long builtRevision = long.MinValue;
        static bool builtStone;
        static readonly PickerFailures failures = new PickerFailures();
        static float openGap;
        static long failureSession = long.MinValue;

        /// <summary>Makes the window the travel host and the receiver of edit answers.</summary>
        public static void Install()
        {
            ThresholdTravel.Host = OpenMode;
            ThresholdTravel.CanHost = CanOpenMode;
            ThresholdTravel.CloseHost = () => { Close(); ThresholdTargetPicker.Close(); };
            ThresholdPortalEdits.Answered = Rebuild;
        }

        public static void Patch(Harmony harmony)
        {
            foreach (var name in PickerMapClicks.PatchedHandlers)
            {
                var method = typeof(Minimap).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == name);
                if (method != null) harmony.Patch(method, prefix: new HarmonyMethod(typeof(ThresholdPickerWindow), nameof(BeforeMapClick)));
            }
        }

        /// <summary>While the picker is open a single map click stays blocked; the game's double click (place a pin) and
        /// right-click removal run as on the ordinary map (4C-11).</summary>
        static bool BeforeMapClick(MethodBase __originalMethod) => PickerMapClicks.NativeRuns(__originalMethod.Name, IsOpen);

        sealed class NativeMapHost
        {
            public RectTransform Panel { get; }
            public bool IsOpen { get; private set; }
            public event Action? Closed;
            public NativeMapHost(RectTransform parent)
            {
                var go = new GameObject("Threshold.Destinations", typeof(RectTransform), typeof(Image));
                Panel = (RectTransform)go.transform;
                Panel.SetParent(parent, false);
                Panel.anchorMin = Panel.anchorMax = new Vector2(0f, 0.5f);
                Panel.pivot = new Vector2(0f, 0.5f);
                Panel.anchoredPosition = new Vector2(12f, 0f);
                Panel.sizeDelta = Size;
                go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
                go.SetActive(false);
            }
            public void Open() { Minimap.instance.SetMapMode(Minimap.MapMode.Large); Panel.gameObject.SetActive(true); IsOpen = true; }
            public void Close()
            {
                if (!IsOpen) return;
                IsOpen = false;
                if (Panel == null) return;
                Panel.gameObject.SetActive(false);
                if (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
                Closed?.Invoke();
            }
            public void Destroy() { Close(); if (Panel != null) UnityEngine.Object.Destroy(Panel.gameObject); }
        }

        /// <summary>From the plugin's Update: the player arrow, a new list from the server, a session that ended, the pad.</summary>
        public static void Tick()
        {
            ThresholdMapClicks.Tick();
            if (window == null || !window.IsOpen) return;
            if (!ThresholdTravel.InSession || Player.m_localPlayer == null || !ThresholdModules.PortalRoutingActive() || ThresholdModules.Mode != TeleportMode.Map) { window.Close(); return; }
            if (PortalTriggerRules.Left(openGap, TriggerGap(ThresholdTravel.Source))) { window.Close(); return; }
            if (Minimap.instance == null || Minimap.instance.m_mode != Minimap.MapMode.Large) { Close(); return; }
            if (ThresholdPortals.Revision != builtRevision || builtStone != (Plugin.Settings?.Get(ThresholdSettings.Picker.HighlightStonePortals) ?? false)) Rebuild();
            try { Pad(); }
            catch (Exception e) { Plugin.Log?.Warning("Threshold: picker pad step failed: " + e.GetType().Name + ": " + e.Message); }
            KeepSelectedRowVisible();
        }

        static void Pad()
        {
            if (!MeadworksInput.GamepadActive || MeadworksInput.Typing || window == null) return;
            if (ExtraPotions.Meadworks.Crucible.UI.WindowInput.GameWindow.AnyOpen) return;
            if (Pressed(PadButton.B)) { Close(); return; }
            if (Pressed(PadButton.Y) && search != null) { search.Focus(); return; }
            if (view == null) return;
            int direction = Pressed(PadButton.DPadDown) ? 1 : Pressed(PadButton.DPadUp) ? -1 : 0;
            var controls = PadControls();
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int at = controls.FindIndex(r => r.gameObject == focused);
            if (direction != 0 && controls.Count > 0)
            {
                at = at < 0 ? (direction > 0 ? 0 : controls.Count - 1) : (at + direction + controls.Count) % controls.Count;
                var button = controls[at];
                int line = rows.IndexOf(button);
                if (line >= 0) Select(line, false);
                EventSystem.current?.SetSelectedGameObject(button.gameObject);
            }
            if (Pressed(PadButton.A) && at >= 0) controls[at].onClick.Invoke();
            if (Pressed(PadButton.X)) TravelSelected();
        }

        /// <summary>The gamepad's button list. Refilled when the rows are rebuilt or a row's interactable flag changes.</summary>
        static List<Button> PadControls()
        {
            CollectPadStamp(padSeen);
            if (padReady && PickerPadList.Unchanged(padStamp, padSeen)) return padControls;
            padStamp.Clear();
            for (int i = 0; i < padSeen.Count; i++) padStamp.Add(padSeen[i]);
            padControls.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row != null && row.interactable) padControls.Add(row);
            }
            AddPad(favourite);
            AddPad(home);
            AddPad(sourceHub);
            AddPad(returnHub);
            AddPad(closeButton);
            padReady = true;
            return padControls;
        }

        static void AddPad(Button? button)
        {
            if (button != null && button.interactable && button.gameObject.activeSelf) padControls.Add(button);
        }

        static void CollectPadStamp(List<int> into)
        {
            into.Clear();
            into.Add(padGeneration);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                into.Add(row == null ? PickerPadList.Missing : row.interactable ? PickerPadList.Ready : PickerPadList.Blocked);
            }
            into.Add(ActionStamp(favourite));
            into.Add(ActionStamp(home));
            into.Add(ActionStamp(sourceHub));
            into.Add(ActionStamp(returnHub));
            into.Add(ActionStamp(closeButton));
        }

        static int ActionStamp(Button? button)
        {
            if (button == null || !button.gameObject.activeSelf) return PickerPadList.Missing;
            return button.interactable ? PickerPadList.Ready : PickerPadList.Blocked;
        }

        static void InvalidatePadList() => padGeneration++;

        static bool Pressed(PadButton button)
        {
            if (!MeadworksInput.PadDown(button)) return false;
            MeadworksInput.PadConsume(button);
            return true;
        }

        /// <summary>A row reached with the D-pad scrolls into the list's view.</summary>
        static void KeepSelectedRowVisible()
        {
            var selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selectedObject == lastSelected) return;
            lastSelected = selectedObject;
            if (selectedObject == null || content == null || viewport == null || !selectedObject.transform.IsChildOf(content)) return;
            if (rows.FindIndex(r => r != null && r.gameObject == selectedObject) < 0) return;
            float top = -((RectTransform)selectedObject.transform).anchoredPosition.y;
            float height = viewport.rect.height;
            float offset = content.anchoredPosition.y;
            if (top < offset) offset = top;
            else if (top + RowHeight > offset + height) offset = top + RowHeight - height;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, Math.Max(0f, offset));
        }

        /// <summary>Whether the picker could open now (the same gates as <see cref="Open"/>, without building anything).</summary>
        static bool OpenMode(TeleportWorld source) => ThresholdModules.Mode == TeleportMode.TargetTeleport ? ThresholdTargetPicker.Open(source) : Open(source);
        static bool CanOpenMode() => ThresholdModules.Mode == TeleportMode.TargetTeleport ? ThresholdTargetPicker.CanOpen() : CanOpen();

        static bool CanOpen()
        {
            var session = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            if (session != failureSession) { failureSession = session; failures.Reset(); }
            return Plugin.AllowsUi && CrucibleUiPlugin.Active && Minimap.instance != null && Minimap.instance.m_mapImageLarge != null && !failures.Disabled;
        }

        static bool Open(TeleportWorld source)
        {
            var session = ExtraPotions.Meadworks.Crucible.Plugin.Session.Generation;
            if (session != failureSession) { failureSession = session; failures.Reset(); }
            if (!Plugin.AllowsUi || !CrucibleUiPlugin.Active || failures.Disabled) return false;
            try
            {
                if (window != null && window.Panel == null) Teardown();
                if (window == null) Build();
                if (window == null) return false;
                selected = -1;
                lastSelected = null;
                if (search != null) search.Text = "";
                openGap = TriggerGap(source);
                window.Open();
                Minimap.instance.ShowPointOnMap(source.transform.position);
                RebuildOrThrow();
                failures.Opened();
                return true;
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }
        }

        /// <summary>A failed open or redraw: tear the window down (the next open builds a fresh one) and let the game's
        /// portal run. Only <see cref="PickerFailures.Limit"/> failures in a row turn the picker off for the session.</summary>
        static void Fail(Exception e)
        {
            bool off = failures.Failed();
            Plugin.Log?.Error("Threshold: the picker window failed" + (off ? " again; it stays off for this session and portals stay the game's: " : "; it is rebuilt on the next open: ") + e);
            try { Teardown(); } catch (Exception again) { Plugin.Log?.Error("Threshold: picker teardown: " + again.GetType().Name); window = null; }
            ThresholdTravel.EndSession();
        }

        static void Close() => window?.Close();

        /// <summary>Meters from the player's center to the portal's walk-in trigger (0 inside it); NaN without a reading,
        /// infinity when the portal is gone (parity #114).</summary>
        static float TriggerGap(TeleportWorld? portal)
        {
            var player = Player.m_localPlayer;
            if (player == null) return float.NaN;
            if (portal == null) return float.PositiveInfinity;
            var trigger = portal.GetComponentInChildren<TeleportWorldTrigger>();
            var collider = trigger != null ? trigger.GetComponent<Collider>() : null;
            if (collider == null || !collider.enabled) return float.NaN;
            var centre = player.GetCenterPoint();
            return Vector3.Distance(collider.ClosestPoint(centre), centre);
        }

        static void Build()
        {
            var minimap = Minimap.instance;
            var parent = minimap != null ? minimap.m_mapImageLarge.rectTransform.parent as RectTransform : null;
            if (parent == null) return;
            window = new NativeMapHost(parent);
            window.Closed += () => { ThresholdTravel.EndSession(); selected = -1; };
            var panel = window.Panel;
            var title = KitCanvas.Text(panel, "Title", ThresholdText.Text(ThresholdTextKeys.PickerTitle), 21, Color.white, TextAlignmentOptions.Left, false);
            if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
            Place((RectTransform)title.transform, new Vector2(18, -14), new Vector2(230, 32));
            var close = KitIconButton.Create(panel, "Close", KitGlyph.Cross, new Vector2(Size.x - 44, -12), 32, _ => Close());
            KitTooltip.Set(close.Button.gameObject, "Threshold", ThresholdText.Text("picker_close_help"));
            closeButton = close.Button;

            search = KitTextField.Create(panel, "Search", KitFieldMode.Search, new Vector2(10, -54), new Vector2(ListWidth - 20, 32), ThresholdText.Text(ThresholdTextKeys.PickerSearch));
            KitTooltip.Set(search.Rect.gameObject, ThresholdText.Text(ThresholdTextKeys.PickerSearch), ThresholdText.Text("picker_search_help"));
            search.Changed += _ => { selected = -1; Rebuild(); };

            var viewportObject = new GameObject("Destinations", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport = (RectTransform)viewportObject.transform;
            viewport.SetParent(panel, false);
            Place(viewport, new Vector2(10, -94), new Vector2(ListWidth - 20, 340));
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
            content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            content.SetParent(viewport, false);
            Place(content, Vector2.zero, new Vector2(ListWidth, 380));
            var scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            emptyText = KitCanvas.Text(viewport, "Empty", ThresholdText.Text(ThresholdTextKeys.PickerEmpty), 15, Color.white, TextAlignmentOptions.Center, true);
            Place((RectTransform)emptyText.transform, new Vector2(10, -150), new Vector2(ListWidth - 20, 60));

            favourite = KitButton.Create(panel, "Favourite", ThresholdText.Text(ThresholdTextKeys.PickerToggleFavourite), ToggleFavourite, new Vector2(10, -442), new Vector2(135, 28));
            home = KitButton.Create(panel, "Home", ThresholdText.Text(ThresholdTextKeys.PickerSetHome), ToggleHome, new Vector2(155, -442), new Vector2(135, 28));
            sourceHub = KitButton.Create(panel, "SourceHub", "", SourceHub, new Vector2(10, -476), new Vector2(280, 28));
            returnHub = KitButton.Create(panel, "ReturnToHub", "", () => ThresholdTravel.ReturnToHub(), new Vector2(10, -510), new Vector2(280, 28));
            KitTooltip.Set(favourite.gameObject, "Threshold", ThresholdText.Text("picker_favorite_help"));
            KitTooltip.Set(home.gameObject, "Threshold", ThresholdText.Text("picker_home_help"));
            KitTooltip.Set(sourceHub.gameObject, "Threshold", ThresholdText.Text("picker_source_hub_help"));
            KitTooltip.Set(returnHub.gameObject, "Threshold", ThresholdText.Text("picker_return_help"));
            var hint = KitCanvas.Text(panel, "MapPinsHint", ThresholdText.Text(ThresholdTextKeys.PickerMapPinsHint), 13, Color.white, TextAlignmentOptions.Center, true);
            hint.raycastTarget = false;
            Place((RectTransform)hint.transform, new Vector2(10, -542), new Vector2(ListWidth - 20, 34));
        }

        /// <summary>Reads the list, the favorites and the home hub again and redraws the list, the map and the buttons.</summary>
        static void Rebuild()
        {
            try { RebuildOrThrow(); }
            catch (Exception e) { Fail(e); }
        }

        static void RebuildOrThrow()
        {
            if (window == null || !window.IsOpen || content == null) return;
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            if (settings == null || player == null) return;
            builtRevision = ThresholdPortals.Revision;
            builtStone = settings.Get(ThresholdSettings.Picker.HighlightStonePortals);
            var keep = view?.Destination(selected);
            var current = ThresholdTravel.SourceEntry;
            var at = player.transform.position;
            var viewer = new AccessViewer(player.GetPlayerID(), ZNet.instance != null && ZNet.instance.LocalPlayerIsAdminOrHost());
            var query = new PickerQuery(ThresholdPortals.Current, current, new MapPoint(at.x, at.y, at.z), viewer,
                settings.Get(ThresholdSettings.Access.AccessModesEnabled), search?.Text ?? "", settings.Get(ThresholdSettings.Picker.Sort), Favourites(player),
                settings.Get(ThresholdSettings.General.ListUntaggedPortals),
                settings.Get(ThresholdSettings.General.OnlyExploredPortals) ? (Func<MapPoint, bool>)ThresholdMapPane.Explored : null,
                PrefabFilter.Parse(settings.Get(ThresholdSettings.General.PortalPrefabs)), builtStone);
            view = PickerView.Build(PickerList.Build(query), current, ThresholdTravel.ReturnHub(), ThresholdHomeHub.Load(),
                settings.Get(ThresholdSettings.Access.AccessModesEnabled), builtStone);
            selected = keep != null ? view.LineOfPin(view.Plan.IndexOf(keep.Id)) : -1;

            DrawRows();
            Select(selected, false);
        }

        static void DrawRows()
        {
            // Destroy is deferred to the end of the frame, and KitButton.Create reuses a child it finds by name. A second
            // redraw in the same frame (open plus a list or search change) used to get the dying "Line_i" rows back, keep
            // them, and throw on the next redraw. Detach and rename old rows first so Create can never find them again.
            foreach (var old in rows)
            {
                if (old == null) continue; // already destroyed by Unity
                var dying = old.gameObject;
                dying.name = "Line_dead";
                dying.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(dying);
            }
            rows.Clear();
            InvalidatePadList();
            if (view == null || content == null) return;
            if (emptyText != null) emptyText.gameObject.SetActive(view.IsEmpty);
            var untagged = ThresholdText.Text(ThresholdTextKeys.PickerUntagged);
            var homeWord = ThresholdText.Text(ThresholdTextKeys.PickerHomeHub);
            for (int i = 0; i < view.Lines.Count; i++)
            {
                var line = view.Lines[i];
                int index = i;
                string caption;
                if (line.Kind == PickerLineKind.Header) caption = ThresholdText.Text(ThresholdTextKeys.Category(line.Category));
                else
                {
                    var row = line.Row!;
                    var label = row.Label.Length > 0 ? row.Label : untagged;
                    if (row.IsFavourite) label = ThresholdText.Format(ThresholdTextKeys.PickerFavouriteLabel, ThresholdText.Text(ThresholdTextKeys.Category(row.Category)), label);
                    if (line.IsHome) label += " (" + homeWord + ")";
                    caption = label + "  " + ThresholdText.Format(ThresholdTextKeys.PickerDistance, Math.Round(row.Distance).ToString("0", CultureInfo.InvariantCulture));
                }
                var button = KitButton.Create(content, "Line_" + i, caption, () => { Select(index, false); TravelSelected(); }, new Vector2(0, -i * RowHeight), new Vector2(ListWidth - 20, RowHeight - 2));
                if (button == null) continue;
                SetCaption(button, caption);
                KitTooltip.Set(button.gameObject, caption, ThresholdText.Text(line.Kind == PickerLineKind.Header ? "picker_category_help" : "picker_destination_help"));
                if (line.Kind == PickerLineKind.Header)
                {
                    button.interactable = false;
                    var colour = MapPinColours.Header(line.Category);
                    var text = button.GetComponentInChildren<TMP_Text>();
                    if (colour != null && text != null) text.color = new Color(colour.Value.R, colour.Value.G, colour.Value.B, colour.Value.A);
                }
                else
                {
                    button.gameObject.AddComponent<ThresholdDestinationContext>().Line = index;
                    var text = button.GetComponentInChildren<TMP_Text>();
                    var colour = MapPinColours.Tint(view.Plan.Pins[line.Pin]);
                    if (text != null) text.color = new Color(colour.R, colour.G, colour.B, colour.A);
                }
                rows.Add(button);
            }
            content.sizeDelta = new Vector2(ListWidth, Math.Max(340f, view.Lines.Count * RowHeight));
        }

        /// <summary>Focus a destination for its management actions; the row click then travels.</summary>
        static void Select(int line, bool centre)
        {
            if (view == null) return;
            var target = view.Destination(line);
            selected = target != null ? line : -1;

            if (favourite != null) favourite.interactable = target != null;
            var homeKey = view.HomeActionKey(selected);
            if (home != null)
            {
                home.gameObject.SetActive(homeKey != null);
                if (homeKey != null) SetCaption(home, ThresholdText.Text(homeKey));
            }
            var hubKey = PickerView.TextKey(view.SourceHub);
            if (sourceHub != null)
            {
                sourceHub.gameObject.SetActive(hubKey != null);
                if (hubKey != null) SetCaption(sourceHub, ThresholdText.Text(hubKey));
            }
            if (returnHub != null)
            {
                returnHub.gameObject.SetActive(view.ReturnHub != null);
                if (view.ReturnHub != null) SetCaption(returnHub, ThresholdText.Format(ThresholdTextKeys.PickerReturnToHub, view.ReturnHub.HubName));
            }
        }

        static void TravelSelected()
        {
            var target = view?.Destination(selected);
            if (target != null) ThresholdTravel.TravelTo(target);
        }

        static void ToggleHome()
        {
            var target = view?.Destination(selected);
            if (target == null || view!.HomeActionKey(selected) == null) return;
            if (view.Lines[selected].IsHome) ThresholdHomeHub.Clear();
            else ThresholdHomeHub.Save(target);
            Rebuild();
        }

        static void SourceHub()
        {
            var source = ThresholdTravel.Source;
            var entry = ThresholdTravel.SourceEntry;
            if (view == null || source == null || entry == null) return;
            if (view.SourceHub == SourceHubAction.SetHub) ThresholdPortalEdits.AskHubName(source);
            else if (view.SourceHub == SourceHubAction.ClearHub) ThresholdPortalEdits.Request(PortalEdit.ClearHub(entry.Id));
        }

        static FavouriteSet Favourites(Player player) =>
            FavouriteSet.Parse(player.m_customData.TryGetValue(FavouriteSet.CharacterDataKey, out var raw) ? raw : null);

        public static void Focus(int line) => Select(line, false);
        public static void Context(int line) { Focus(line); ToggleFavourite(); }

        static void ToggleFavourite()
        {
            var player = Player.m_localPlayer;
            var target = view?.Destination(selected);
            if (player == null || target == null) return;
            var set = Favourites(player);
            set.Toggle(PortalKey.Of(target.Position));
            player.m_customData[FavouriteSet.CharacterDataKey] = set.Format();
            Rebuild();
        }

        static void SetCaption(Button button, string caption)
        {
            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = caption;
        }

        static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        public static void Shutdown()
        {
            if (ThresholdTravel.Host == (Func<TeleportWorld, bool>)OpenMode) ThresholdTravel.Host = null;
            if (ThresholdTravel.CanHost == (Func<bool>)CanOpenMode) ThresholdTravel.CanHost = null;
            ThresholdTravel.CloseHost = null;
            Teardown();
            failures.Reset();
            failureSession = long.MinValue;
        }

        /// <summary>Destroys the window and its parts but keeps the picker installed as the travel host.</summary>
        static void Teardown()
        {

            search?.Destroy();
            search = null;
            rows.Clear();
            padControls.Clear();
            padReady = false;
            InvalidatePadList();
            window?.Destroy();
            window = null;
            content = null;
            viewport = null;
            lastSelected = null;
            emptyText = null;
            closeButton = favourite = home = sourceHub = returnHub = null;
            view = null;
            selected = -1;
            builtRevision = long.MinValue;
        }
    }
}

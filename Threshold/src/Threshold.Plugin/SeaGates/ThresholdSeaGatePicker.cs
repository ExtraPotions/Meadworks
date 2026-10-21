// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the helmsman's sea gate picker on the Crucible.UI kit (in place of Wayfare's large-map picker).
// Behavior as Wayfare's SeaGatePicker (GPL-3.0-or-later, the reference): only the helmsman picks; closing sails on.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Threshold.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Threshold.Runtime.SeaGates
{
    /// <summary>
    /// While the ship the local player steers is stopped in a gate (<see cref="JumpState.Choosing"/>), a kit window lists
    /// every other sea gate the server lets this player sail to (<see cref="SeaGateIndex"/>), nearest first. A row sends
    /// the pick to the ship's owner (<see cref="SeaGateFields.PickRpc"/>); the window stays open until the jump begins,
    /// so after a denial the helmsman picks again. Closing it (the close button, B, Escape or Sail on) sails on through.
    /// A crew member who is not at the helm is told once per stop who picks. D-pad and A move through the rows (the kit's
    /// pad grid), B closes.
    /// </summary>
    public static class ThresholdSeaGatePicker
    {
        const float RowHeight = 34f;
        const float Width = 420f;
        const float ListHeight = 340f;
        const float RequestSeconds = 3f;

        static KitWindow? window;
        static RectTransform? content;
        static TMP_Text? emptyText;
        static Button? sailOn;
        static readonly List<Button> rows = new List<Button>();
        static Ship? ship;
        static int stopId;
        static long sourceId;
        static int closedStop;
        static int toldStop;
        static bool quiet;
        static float requestedAt;
        static int builtFrom = -1;

        /// <summary>From the plugin's Update on a graphical client.</summary>
        public static void Tick()
        {
            var player = Player.m_localPlayer;
            if (!SeaGateConfig.On || player == null || !CrucibleUiPlugin.Active)
            {
                CloseQuiet();
                return;
            }
            var steered = player.GetControlledShip();
            var stop = StopOf(steered);
            if (window != null && window.IsOpen)
            {
                if (stop == null || steered != ship || stop.GetInt(SeaGateFields.StopKey, 0) != stopId) { CloseQuiet(); return; }
                if (Time.time - requestedAt > RequestSeconds) Request();
                if (Fingerprint() != builtFrom) Draw();
                return;
            }
            if (stop == null)
            {
                TellCrew(player);
                return;
            }
            int id = stop.GetInt(SeaGateFields.StopKey, 0);
            if (id != closedStop) Open(steered!, id, stop.GetLong(SeaGateFields.StopGateKey, 0L));
        }

        static void Open(Ship stopped, int id, long gate)
        {
            try
            {
                if (window == null) Build();
                if (window == null) return;
                ship = stopped;
                stopId = id;
                sourceId = gate;
                builtFrom = -1;
                window.Open();
                Request();
                Draw();
            }
            catch (Exception e)
            {
                SeaGateLog.LogError("the sea gate picker failed; the ship sails on: " + e);
                closedStop = id;
                Send(stopped, id, 0L);
                Teardown();
            }
        }

        static void Build()
        {
            window = KitWindow.Create(Plugin.Guid + ".seagates", new Vector2(Width, ListHeight + 150f), typing: false, round: true);
            if (window == null) return;
            window.Closed += OnClosed;
            var panel = window.Panel;
            var title = KitCanvas.Text(panel, "Title", ThresholdText.Text(ThresholdTextKeys.SgPickerTitle), 21, Color.white, TextAlignmentOptions.Left, false);
            if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
            Place((RectTransform)title.transform, new Vector2(18, -14), new Vector2(Width - 70, 32));
            var close = KitIconButton.Create(panel, "Close", KitGlyph.Cross, new Vector2(Width - 44, -12), 32, _ => window?.Close());
            window.Focusable(close.Button, 1, 0);

            var viewportObject = new GameObject("Gates", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            var viewport = (RectTransform)viewportObject.transform;
            viewport.SetParent(panel, false);
            Place(viewport, new Vector2(18, -56), new Vector2(Width - 36, ListHeight));
            viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
            content = (RectTransform)new GameObject("Content", typeof(RectTransform)).transform;
            content.SetParent(viewport, false);
            Place(content, Vector2.zero, new Vector2(Width - 36, ListHeight));
            var scroll = viewportObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            emptyText = KitCanvas.Text(viewport, "Empty", ThresholdText.Text(ThresholdTextKeys.SgPickerEmpty), 15, Color.white, TextAlignmentOptions.Center, true);
            Place((RectTransform)emptyText.transform, new Vector2(10, -140), new Vector2(Width - 56, 60));

            sailOn = KitButton.Create(panel, "SailOn", ThresholdText.Text(ThresholdTextKeys.SgSailOn), () => window?.Close(), new Vector2(18, -ListHeight - 76), new Vector2(Width - 36, 40));
        }

        /// <summary>The gates this player may sail to, nearest to the ship first; the gate the ship is in left out.</summary>
        static void Draw()
        {
            if (window == null || content == null || ship == null) return;
            builtFrom = Fingerprint();
            foreach (var old in rows)
            {
                if (old == null) continue;
                window.Unfocusable(old);
                var dying = old.gameObject;
                dying.name = "Gate_dead";
                dying.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(dying);
            }
            rows.Clear();
            var at = ship.transform.position;
            var gates = SeaGateIndex.Gates.Where(g => g.Id != sourceId).OrderBy(g => Flat(g.Position, at)).ToList();
            if (emptyText != null) emptyText.gameObject.SetActive(gates.Count == 0);
            var fallbackName = global::Localization.instance != null ? global::Localization.instance.Localize(SeaGateWords.DefaultName) : "Sea gate";
            for (int i = 0; i < gates.Count; i++)
            {
                long id = gates[i].Id;
                var name = string.IsNullOrEmpty(gates[i].Name) ? fallbackName : gates[i].Name.RemoveRichTextTags().Trim();
                var caption = name + "  " + ThresholdText.Format(ThresholdTextKeys.PickerDistance, Math.Round(Flat(gates[i].Position, at)).ToString("0", CultureInfo.InvariantCulture));
                var button = KitButton.Create(content, "Gate_" + i, caption, () => Pick(id), new Vector2(0, -i * RowHeight), new Vector2(Width - 36, RowHeight - 2));
                if (button == null) continue;
                var text = button.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = caption;
                window.Focusable(button, 0, i + 1);
                rows.Add(button);
            }
            content.sizeDelta = new Vector2(Width - 36, Math.Max(ListHeight, gates.Count * RowHeight));
            if (sailOn != null)
            {
                window.Unfocusable(sailOn);
                window.Focusable(sailOn, 0, rows.Count + 1);
            }
        }

        static int Fingerprint()
        {
            unchecked
            {
                int hash = 17;
                foreach (var gate in SeaGateIndex.Gates) hash = hash * 31 + gate.Id.GetHashCode() + (gate.Name ?? "").GetHashCode();
                return hash * 31 + SeaGateIndex.Gates.Count;
            }
        }

        static void Pick(long gateId)
        {
            if (ship != null && gateId != sourceId) Send(ship, stopId, gateId);
        }

        /// <summary>The helmsman closed the window: the ship sails on through, and this stop does not open it again.</summary>
        static void OnClosed()
        {
            if (quiet) return;
            closedStop = stopId;
            if (ship != null) Send(ship, stopId, 0L);
        }

        static void CloseQuiet()
        {
            if (window == null || !window.IsOpen) return;
            quiet = true;
            try { window.Close(); }
            finally { quiet = false; }
            ship = null;
        }

        static void Request()
        {
            requestedAt = Time.time;
            SeaGateIndex.RequestFromServer();
        }

        static void Send(Ship target, int stop, long gateId)
        {
            var view = target != null ? target.m_nview : null;
            if (view != null && view.IsValid()) view.InvokeRPC(SeaGateFields.PickRpc, stop, gateId);
        }

        /// <summary>A crew member aboard a stopped ship (not at the helm) learns once per stop who picks.</summary>
        static void TellCrew(Player player)
        {
            var aboard = Ship.GetLocalShip();
            var stop = StopOf(aboard);
            if (stop == null) return;
            int id = stop.GetInt(SeaGateFields.StopKey, 0);
            if (id == toldStop) return;
            toldStop = id;
            var controls = aboard!.m_shipControlls;
            long user = controls != null ? controls.GetUser() : 0L;
            var helmsman = user != 0L ? Player.GetPlayer(user) : null;
            var name = helmsman != null ? helmsman.GetPlayerName() : ThresholdText.Text(ThresholdTextKeys.SgHelmsman);
            player.Message(MessageHud.MessageType.TopLeft, ThresholdText.Format(ThresholdTextKeys.SgCrewWait, name));
        }

        static ZDO? StopOf(Ship? stopped)
        {
            var view = stopped != null ? stopped.m_nview : null;
            var zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null && SeaGateFields.GetState(zdo) == JumpState.Choosing ? zdo : null;
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
        }

        static void Teardown()
        {
            rows.Clear();
            window?.Destroy();
            window = null;
            content = null;
            emptyText = null;
            sailOn = null;
            ship = null;
        }

        public static void Shutdown()
        {
            Teardown();
            closedStop = 0;
            toldStop = 0;
        }
    }
}

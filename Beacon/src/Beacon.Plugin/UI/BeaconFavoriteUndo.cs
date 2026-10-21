// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a timed Undo notice on the existing kit panel/window; no native favorite or secret is copied.
using ExtraPotions.Beacon.Servers;
using ExtraPotions.Meadworks.Crucible.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Beacon.Runtime
{
    internal static class BeaconFavoriteUndo
    {
        static KitWindow? window;
        static ServerRemoval? removal;
        static ServerAddress? favorite;
        static TMP_Text? text;
        static Button? undo;
        static float expires;
        public static void Show(ServerRemoval removed, ServerAddress address)
        { Reset(); removal = removed; favorite = address; Create(BeaconText.Format("favorite_removed", removed.Entry.Name)); }
        public static void Failure(string message) { Reset(); Create(message); }
        static void Create(string message)
        {
            var canvas = FejdStartup.instance?.m_mainMenu?.GetComponentInParent<Canvas>()?.rootCanvas;
            if (canvas == null) { Reset(); return; }
            window = KitWindow.Create("Beacon_FavoriteUndo", new Vector2(540, 150), canvas.transform);
            if (window == null) { Reset(); return; }
            window.Panel.anchorMin = window.Panel.anchorMax = window.Panel.pivot = new Vector2(1, 0);
            window.Panel.anchoredPosition = new Vector2(-20, 20);
            text = KitAlmanacDialog.Label(window.Panel, "Message", message, new Vector2(20, -12), new Vector2(500, 64), 16);
            if (removal != null)
            {
                undo = KitButton.Create(window.Panel, "Undo", BeaconPanel.Ui("undo"), Undo, new Vector2(20, -96), new Vector2(240, 36));
                window.Focusable(undo, 0, 0);
                BeaconPanel.Tip(undo.gameObject,"undo");
            }
            var dismiss = KitButton.Create(window.Panel, "Dismiss", BeaconPanel.Ui("close"), Reset, new Vector2(280, -96), new Vector2(240, 36));
            BeaconPanel.Tip(dismiss.gameObject,"close");
            window.Focusable(dismiss, 1, 0); window.Closed += Reset; window.Open(); expires = Time.unscaledTime + 15;
        }
        static void Undo()
        {
            if (removal == null || favorite == null) return;
            if (BeaconFavorites.Undo(removal, favorite)) Reset();
            else if (text != null) text.text = BeaconPanel.Ui("undo_conflict");
        }
        public static void Tick()
        {
            if (window == null) return;
            if (Game.instance != null || FejdStartup.instance == null || Time.unscaledTime >= expires) { Reset(); return; }
            if (undo != null) undo.interactable = !BeaconImportSecrets.Busy;
        }
        public static void Reset()
        {
            var old = window; window = null; removal = null; favorite = null; text = null; undo = null;
            if (old != null) { old.Closed -= Reset; old.Close(); old.Destroy(); }
        }
    }
}

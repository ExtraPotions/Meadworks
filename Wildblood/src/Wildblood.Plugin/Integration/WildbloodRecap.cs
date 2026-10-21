// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Product ownership over the adopted recap capture, JPEG, hit-log and replay data.
using System;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Wildblood.Adopted.Recap;
using ExtraPotions.Wildblood.Adopted.Recap.Window;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Runtime;

internal static class WildbloodRecap
{
    static long generation = -1, profile;
    static Player? actor;
    static FrameGrabber? capture;
    static KitWindow? window;
    static TMP_Text? title, summary, clock, hits, empty;
    static RawImage? image;
    static Texture2D? texture;
    static Button? play;
    static KitAlmanacNumber? timeline;
    static DeathRecap? selected;
    static int death, frame = -1;
    static float position;
    static bool playing, changed;
    internal static bool IsOpen => window?.IsOpen == true;
    static bool OwnerAvailable => Plugin.NativeRulesReady && Plugin.AllowsUi && WildbloodModules.Active(WildbloodModule.Recap)
        && CruciblePlugin.Session.AuthorityReady && CruciblePlugin.Session.WorldId != 0
        && ZNet.instance != null && ZNet.m_world != null && ZNet.instance.GetWorldUID() == CruciblePlugin.Session.WorldId;
    static bool Available => OwnerAvailable && Player.m_localPlayer != null && Player.m_localPlayer.GetPlayerID() != 0
        && Player.m_localPlayer.m_nview != null && Player.m_localPlayer.m_nview.IsValid() && Player.m_localPlayer.m_nview.IsOwner();
    internal static bool Recording => Available && Plugin.Settings!.Get(WildbloodSettings.Recap.RecordDeaths)
        && actor == Player.m_localPlayer && generation == CruciblePlugin.Session.Generation && profile == Player.m_localPlayer.GetPlayerID();

    internal static void Tick(GameObject host)
    {
        if (!OwnerAvailable) { Reset(); return; }
        bool sessionChanged = generation != CruciblePlugin.Session.Generation;
        if (sessionChanged) Reset();
        if (!Available)
        {
            StopCapture(); RecapStore.CancelPending(); HitLog.Clear(); FrameRing.Clear();
            actor = null; generation = CruciblePlugin.Session.Generation; window?.Close();
            return; // A respawning actor may be absent; completed same-profile/session histories remain owned.
        }
        long nextProfile = Player.m_localPlayer.GetPlayerID();
        if (profile != 0 && profile != nextProfile) { Reset(); sessionChanged = true; }
        if (sessionChanged || actor != Player.m_localPlayer)
        {
            StopCapture(); RecapStore.CancelPending(); HitLog.Clear(); FrameRing.Clear();
            generation = CruciblePlugin.Session.Generation; profile = nextProfile; actor = Player.m_localPlayer;
        }
        if (Recording)
        {
            if (capture == null)
            {
                FrameEncoder.Start();
                try { capture = host.AddComponent<FrameGrabber>(); }
                catch { FrameEncoder.Stop(); throw; }
            }
            RecapStore.Update();
        }
        else if (capture != null) { StopCapture(); RecapStore.CancelPending(); HitLog.Clear(); FrameRing.Clear(); }
        var key = Plugin.Settings!.Shortcut(WildbloodSettings.Recap.RecapKey);
        if (MeadworksInput.Pressed(key, IsOpen ? InputContext.Window : InputContext.Gameplay))
        { if (IsOpen) window!.Close(); else Open(); }
        if (!IsOpen) return;
        if (changed || selected != null && !RecapStore.Deaths.Contains(selected)) { changed = false; Select(death); }
        PadScrub();
        if (playing && selected != null)
        {
            position = Mathf.Min(selected.Duration, position + Time.unscaledDeltaTime);
            if (position >= selected.Duration) playing = false;
        }
        DrawReplay();
    }
    static void StopCapture()
    {
        FrameEncoder.Stop();
        if (capture != null) { capture.enabled = false; UnityEngine.Object.Destroy(capture); }
        capture = null;
    }
    static string T(string key) => WildbloodText.Text("recap_" + key);
    static string F(string key, params object[] args) => WildbloodText.Format("recap_" + key, args);
    static string Literal(string? text) => text == null ? "" : text.Length > 256 ? text.Substring(0, 256) : text;
    static TMP_Text Label(string name, string value, Vector2 at, Vector2 size, float font = 16)
    {
        var label = KitAlmanacDialog.Label(window!.Panel, name, value, at, size, font);
        label.overflowMode = TextOverflowModes.Truncate;
        return label;
    }
    static Button Button(string id, string caption, Action click, Vector2 at, Vector2 size, int x, int y)
    {
        var button = KitButton.Create(window!.Panel, id, caption, () => { if (Available) click(); }, at, size);
        string help = id == "EarlierDeath" ? "older" : id == "LaterDeath" ? "newer" : id == "Back" ? "back" : id == "Forward" ? "forward"
            : id == "Death" ? "death" : id == "PriorHit" ? "previous_hit" : id == "NextHit" ? "next_hit" : id == T("play") ? "play" : "close";
        KitTooltip.SetMenu(button.gameObject, caption, T("help_" + help));
        window.Focusable(button, x, y); return button;
    }
    /// <summary>The MeadWorks menu lists the recap while it could open.</summary>
    internal static bool MenuAvailable => Available;
    internal static void OpenFromMenu() { if (!IsOpen) Open(); }
    internal static void Open()
    {
        if (!Available || KitCanvas.Font == null) return;
        if (window?.Panel == null)
        {
            window = KitWindow.Create("WildbloodRecap", new Vector2(900, 600));
            if (window == null) return;
            WildbloodInsets.Add(window.Panel, "VideoInset", new Vector2(20, -104), new Vector2(580, 300));
            WildbloodInsets.Add(window.Panel, "HitsInset", new Vector2(620, -104), new Vector2(260, 300));
            title = Label("Title", "Wildblood · " + T("title"), new Vector2(20, -12), new Vector2(650, 32), 24);
            if (KitCanvas.Heading != null) title.font = KitCanvas.Heading;
            Button(T("close"), T("close"), () => window?.Close(), new Vector2(740, -12), new Vector2(140, 32), 6, 0);
            Button("EarlierDeath", T("older"), () => Select(death + 1), new Vector2(20, -54), new Vector2(150, 32), 0, 1);
            Button("LaterDeath", T("newer"), () => Select(death - 1), new Vector2(180, -54), new Vector2(150, 32), 1, 1);
            summary = Label("Summary", "", new Vector2(350, -54), new Vector2(530, 50), 14);
            var bounds = new GameObject("VideoBounds", typeof(RectTransform)); bounds.transform.SetParent(window.Panel, false);
            KitAlmanacDialog.Place((RectTransform)bounds.transform, new Vector2(20, -104), new Vector2(580, 300));
            var node = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)); node.transform.SetParent(bounds.transform, false);
            image = node.GetComponent<RawImage>(); image.raycastTarget = false;
            node.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            empty = Label("VideoStatus", T("no_video"), new Vector2(40, -214), new Vector2(540, 80), 16);
            hits = Label("Hits", "", new Vector2(620, -104), new Vector2(260, 300), 15);
            clock = Label("Clock", "", new Vector2(20, -410), new Vector2(860, 28), 16);
            timeline = KitAlmanacNumber.Create(window.Panel, "Timeline", new Vector2(20, -444), new Vector2(860, 76), "0",
                text => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value)
                    && !float.IsInfinity(value) && value >= 0f && value <= (selected?.Duration ?? 0f),
                text => { if (Available && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) Seek(value); },
                direction => Mathf.Clamp(position + direction, 0f, selected?.Duration ?? 0f).ToString("0.0", CultureInfo.InvariantCulture),
                0, 32, .1, serialize: value => value.ToString("0.0", CultureInfo.InvariantCulture));
            window.Focusable(timeline.Less, 0, 2); window.Focusable(timeline.Field.Input, 1, 2); window.Focusable(timeline.More, 2, 2);
            KitTooltip.SetMenu(timeline.Less.gameObject, T("back"), T("help_back"));
            KitTooltip.SetMenu(timeline.More.gameObject, T("forward"), T("help_forward"));
            KitTooltip.SetMenu(timeline.Field.Input.gameObject, T("title"), T("help_time"));
            if (timeline.Slider != null)
            {
                // The slider is a copy of a game slider, which can carry its own scale; at scale 1 it is exactly the 800 px track under the field.
                timeline.Slider.transform.localScale = Vector3.one;
                // The track spans the selected clip, not the field's fixed 0-32 s range; dragging seeks directly.
                timeline.Slider.onValueChanged.RemoveAllListeners();
                timeline.Slider.onValueChanged.AddListener(share => { if (Available && selected != null) Seek(share * selected.Duration); });
                window.Focusable(timeline.Slider, 3, 2);
                KitTooltip.SetMenu(timeline.Slider.gameObject, T("title"), T("help_timeline"));
            }
            play = Button(T("play"), T("play"), () => { if (selected != null) { if (position >= selected.Duration) Seek(0); playing = !playing; } },
                new Vector2(20, -536), new Vector2(135, 38), 0, 3);
            Button("Back", T("back"), () => Seek(position - 1), new Vector2(165, -536), new Vector2(135, 38), 1, 3);
            Button("Forward", T("forward"), () => Seek(position + 1), new Vector2(310, -536), new Vector2(135, 38), 2, 3);
            Button("Death", T("death"), () => Seek(selected?.DeathAt ?? 0), new Vector2(455, -536), new Vector2(135, 38), 3, 3);
            Button("PriorHit", T("previous_hit"), () => SeekHit(-1), new Vector2(600, -536), new Vector2(135, 38), 4, 3);
            Button("NextHit", T("next_hit"), () => SeekHit(1), new Vector2(745, -536), new Vector2(135, 38), 5, 3);
            window.Closed += () => playing = false;
            RecapStore.Changed += OnChanged;
        }
        window.Open(); Select(0);
        if (play != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(play.gameObject);
    }
    static void OnChanged() => changed = true;
    static void Select(int index)
    {
        death = Mathf.Clamp(index, 0, Math.Max(0, RecapStore.Deaths.Count - 1));
        selected = RecapStore.Deaths.Count == 0 ? null : RecapStore.Deaths[death];
        playing = false; frame = -1; position = 0;
        if (texture != null) { UnityEngine.Object.Destroy(texture); texture = null; }
        if (image != null) image.texture = null;
        DrawReplay();
    }
    static void Seek(float value) { playing = false; position = Mathf.Clamp(value, 0, selected?.Duration ?? 0); DrawReplay(); }
    static void SeekHit(int direction)
    {
        if (selected == null || selected.Hits.Count == 0) return;
        int index = selected.HitAt(position);
        if (direction < 0 && index >= 0 && Mathf.Abs(selected.HitTime(index) - position) < .05f) index--;
        else if (direction > 0) index++;
        Seek(selected.HitTime(Mathf.Clamp(index, 0, selected.Hits.Count - 1)));
    }
    static void PadScrub()
    {
        if (!MeadworksInput.Allows(InputContext.Window) || MeadworksInput.Typing || KitAlmanacDialog.Active != null || window == null) return;
        var current = EventSystem.current?.currentSelectedGameObject;
        if (current == null || !current.activeInHierarchy || !current.transform.IsChildOf(window.Panel)) return;
        var parent = window.Panel.parent;
        for (int i = window.Panel.GetSiblingIndex() + 1; parent != null && i < parent.childCount; i++)
            if (parent.GetChild(i).gameObject.activeInHierarchy) return;
        if (MeadworksInput.PadDown(PadButton.LeftBumper)) { MeadworksInput.PadConsume(PadButton.LeftBumper); Seek(position - 1); }
        else if (MeadworksInput.PadDown(PadButton.RightBumper)) { MeadworksInput.PadConsume(PadButton.RightBumper); Seek(position + 1); }
    }
    static void DrawReplay()
    {
        if (window == null || title == null || summary == null || hits == null || clock == null || empty == null || image == null) return;
        title.text = selected == null ? "Wildblood · " + T("title") : "Wildblood · " + Literal(selected.Killer);
        summary.text = selected == null ? T("no_deaths") : F("summary", F("when", selected.Day, selected.Clock.ToString("HH:mm", CultureInfo.InvariantCulture)), death + 1,
                RecapStore.Deaths.Count, Math.Round(selected.Hits.Sum(hit => hit.Damage)));
        int wanted = selected?.FrameAt(position) ?? -1;
        if (selected != null && wanted >= 0 && wanted != frame)
        {
            texture ??= RecapPictures.NewTexture("WildbloodRecapPlayback");
            if (RecapPictures.Show(texture, selected.Frames[wanted]))
            { image.texture = texture; image.GetComponent<AspectRatioFitter>().aspectRatio = texture.width / (float)Math.Max(1, texture.height); frame = wanted; }
        }
        image.enabled = selected?.HasVideo == true && image.texture != null;
        empty.gameObject.SetActive(!image.enabled);
        clock.text = F("clock", RecapText.Clip(position), RecapText.Clip(selected?.Duration ?? 0), RecapText.Clip(selected?.DeathAt ?? 0));
        timeline?.Set(position.ToString("0.0", CultureInfo.InvariantCulture));
        timeline?.Slider?.SetValueWithoutNotify(selected == null || selected.Duration <= 0f ? 0f : position / selected.Duration);
        if (play != null) { string caption = playing ? T("pause") : T("play"); GameButtons.Caption(play.gameObject, caption); KitTooltip.SetMenu(play.gameObject, caption, T("help_play")); }
        if (selected == null) { hits.text = F("hits", 0); return; }
        int at = selected.HitAt(position);
        hits.text = F("hits", selected.Hits.Count) + "\n" + string.Join("\n\n", selected.Hits.Skip(Math.Max(0, at - 2)).Take(5).Select(hit =>
            RecapText.Clip(hit.Time - selected.Start) + " · " + Literal(hit.Attacker.Length > 0 ? hit.Attacker : hit.Source)
            + "\n" + F("damage", Math.Round(hit.Damage), Math.Ceiling(hit.Health), Math.Round(hit.MaxHealth))
            + "\n" + string.Join(" · ", hit.Parts.Take(4).Select(part => Literal(part.Key) + " " + Math.Round(part.Value)))));
    }
    internal static void Reset()
    {
        if (generation < 0 && capture == null && window == null) return;
        StopCapture(); RecapStore.Reset(); HitLog.Clear(); FrameRing.Clear();
        RecapStore.Changed -= OnChanged;
        timeline?.Dispose(); timeline = null;
        if (window != null) { window.Close(); window.Destroy(); window = null; }
        if (texture != null) UnityEngine.Object.Destroy(texture);
        texture = null; selected = null; actor = null; generation = -1; profile = 0;
        image = null; title = summary = clock = hits = empty = null; play = null;
        playing = changed = false; death = 0; frame = -1; position = 0;
    }
}

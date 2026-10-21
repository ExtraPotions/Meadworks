// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration. 2026-10-13: forged inset behind the board.
using System;
using System.Globalization;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Runtime;
using ExtraPotions.Wildblood.Adopted.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Plugin = ExtraPotions.Wildblood.Runtime.Plugin;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Wildblood.Adopted.Tally;

internal static class BossBoardView
{
    const int PageSize = 12;
    static KitHudPanel? panel;
    static Transform? hudOwner;
    static TMP_Text? preview;
    static KitWindow? window;
    static TMP_Text? heading, pageText;
    static Button? previous, next, open;
    static readonly TMP_Text?[] names = new TMP_Text?[PageSize], damage = new TMP_Text?[PageSize];
    static BossBoard? observed, selected, cachedBoard;
    static DamageTally.Entry[] cachedRows = Array.Empty<DamageTally.Entry>();
    static string previewText = "";
    static int drawnPage = -1, language = -1;
    static Terminal.ConsoleCommand? command;
    static Action<BossBoard?>? pending;
    static Terminal? waitingContext;
    static Player? waitingActor;
    static ZRoutedRpc? waitingBus;
    static long waitingProfile;
    static long generation = -1;
    static int epoch, page;
    static float hideAt, waitingUntil;
    static bool explicitPreview;
    static bool Available => EnemyHudPatch.Available && WildbloodModules.Active(WildbloodModule.Bosses);
    static string T(string key) => WildbloodText.Text("board_" + key);
    static string F(string key, params object[] values) => WildbloodText.Format("board_" + key, values);
    static string Literal(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        string bounded = value!.Length > 256 ? value.Substring(0, 256) : value;
        return new string(bounded.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
    }

    internal static void Tick()
    {
        EnsureCommand();
        if (!Available || generation != CruciblePlugin.Session.Generation)
        {
            Reset(); generation = CruciblePlugin.Session.Generation;
            if (!Available) return;
        }
        if (pending != null)
        {
            if (waitingActor == null || waitingActor != Player.m_localPlayer || waitingActor.GetPlayerID() != waitingProfile || !ReferenceEquals(waitingBus, ZRoutedRpc.instance)) CancelRequest();
            else if (Time.unscaledTime >= waitingUntil)
            {
                var context = waitingContext; CancelRequest();
                if (context != null) context.AddString(T("timeout"));
            }
        }
        BossBoard? latest = BossBoard.Latest;
        if (!ReferenceEquals(latest, observed))
        {
            observed = latest;
            if (latest != null) Show(latest);
        }
        if (language != WildbloodText.LanguageVersion)
        {
            language = WildbloodText.LanguageVersion; drawnPage = -1; previewText = "";
            if (open != null) GameButtons.Caption(open.gameObject, T("open"));
        }
        if (window != null && window.IsOpen && drawnPage != page) DrawWindow();
        Transform? root = Hud.instance != null ? Hud.instance.m_rootObject?.transform : null;
        if (panel != null && (panel.Root == null || hudOwner != root)) ReleasePanel();
        bool visible = selected != null && Time.time < hideAt && root != null && Player.m_localPlayer != null
            && !Hud.IsUserHidden() && !Minimap.IsOpen() && !InventoryGui.IsVisible() && !Menu.IsVisible() && !KitWindows.HudBlocked
            && window?.IsOpen != true && (explicitPreview || Plugin.Settings!.Get(WildbloodSettings.Bosses.DamageBoard));
        if (!visible) { panel?.Update(false, false, 1f, 1f); return; }
        if (panel == null && !BuildPanel(root!)) return;
        if (previewText.Length == 0) previewText = Preview(selected!);
        if (preview!.text != previewText) preview.text = previewText;
        panel!.Update(true, ZCursor.LockState != CursorLockMode.Locked && ZCursor.IsVisible, 1f, 1f);
    }

    public static void Show(BossBoard board)
    {
        if (!Available || !Plugin.Settings!.Get(WildbloodSettings.Bosses.DamageBoard)) return;
        Select(board); explicitPreview = false;
        hideAt = Time.time + Plugin.Settings.Get(WildbloodSettings.Display.DamageBoardSeconds);
    }

    public static bool Replay(BossBoard board)
    {
        if (!Available || !CrucibleUiPlugin.Active || KitCanvas.Font == null || KitCanvas.Front == null) return false;
        Select(board); observed = board; explicitPreview = true; page = 0;
        hideAt = Time.time + Plugin.Settings!.Get(WildbloodSettings.Display.DamageBoardSeconds);
        if (window == null)
        {
            window = KitWindow.Create("WildbloodBossBoard", new Vector2(720, 680));
            if (window == null) return false;
            WildbloodInsets.Add(window.Panel, "BoardInset", new Vector2(20, -76), new Vector2(680, 500));
            heading = Label(window.Panel, "Heading", new Vector2(20, -18), new Vector2(680, 48), 21);
            if (KitCanvas.Heading != null) heading.font = KitCanvas.Heading;
            for (int i = 0; i < PageSize; i++)
            {
                names[i] = Label(window.Panel, "Player" + i, new Vector2(30, -82 - i * 40), new Vector2(490, 34), 17);
                damage[i] = Label(window.Panel, "Damage" + i, new Vector2(530, -82 - i * 40), new Vector2(160, 34), 17);
                damage[i]!.alignment = TextAlignmentOptions.TopRight;
            }
            previous = KitButton.Create(window.Panel, "Previous", T("previous"), () => ChangePage(-1), new Vector2(20, -600), new Vector2(170, 38));
            next = KitButton.Create(window.Panel, "Next", T("next"), () => ChangePage(1), new Vector2(200, -600), new Vector2(170, 38));
            window.Focusable(previous, 0, 0); window.Focusable(next, 1, 0);
            pageText = Label(window.Panel, "Page", new Vector2(390, -600), new Vector2(300, 38), 16);
        }
        window.Open(); DrawWindow(); return true;
    }

    static void ChangePage(int direction)
    {
        if (!Available || selected == null || window?.IsOpen != true) return;
        int pages = Math.Max(1, (selected.Entries.Count + PageSize - 1) / PageSize);
        page = Mathf.Clamp(page + direction, 0, pages - 1); DrawWindow();
    }

    static TMP_Text Label(Transform parent, string id, Vector2 at, Vector2 size, float font)
    {
        var label = KitAlmanacDialog.Label(parent, id, "", at, size, font);
        label.richText = false; label.overflowMode = TextOverflowModes.Truncate;
        return label;
    }
    static string BossName(BossBoard board) => Literal(global::Localization.instance != null
        ? global::Localization.instance.Localize(board.BossName) : board.BossName);
    static void Select(BossBoard board)
    {
        selected = board; drawnPage = -1; previewText = "";
        if (!ReferenceEquals(cachedBoard, board))
        {
            cachedBoard = board;
            cachedRows = board.Entries.OrderByDescending(row => row.Damage).ThenBy(row => row.Id).Take(128).ToArray();
        }
    }
    static DamageTally.Entry[] Rows(BossBoard board) => ReferenceEquals(board, cachedBoard) ? cachedRows : Array.Empty<DamageTally.Entry>();
    static string Amount(float value) => value.ToString("N0", CultureInfo.CurrentCulture);
    static string Preview(BossBoard board)
    {
        var rows = Rows(board);
        return F("defeated", BossName(board)) + "\n" + string.Join("\n", rows.Take(6).Select(row =>
            Literal(row.Name) + "   " + Amount(row.Damage))) + (rows.Length > 6 ? "\n" + F("more", rows.Length - 6) : "");
    }
    static void DrawWindow()
    {
        if (selected == null || heading == null || pageText == null) return;
        var rows = Rows(selected);
        int pages = Math.Max(1, (rows.Length + PageSize - 1) / PageSize);
        page = Mathf.Clamp(page, 0, pages - 1);
        drawnPage = page;
        if (previous != null) GameButtons.Caption(previous.gameObject, T("previous"));
        if (next != null) GameButtons.Caption(next.gameObject, T("next"));
        heading.text = "Wildblood · " + F("defeated", BossName(selected));
        pageText.text = F("page", page + 1, pages);
        for (int i = 0; i < PageSize; i++)
        {
            int index = page * PageSize + i;
            names[i]!.text = index < rows.Length ? Literal(rows[index].Name) : "";
            damage[i]!.text = index < rows.Length ? Amount(rows[index].Damage) : "";
        }
    }
    static bool BuildPanel(Transform root)
    {
        if (!CrucibleUiPlugin.Active || KitCanvas.Font == null) return false;
        float height = root is RectTransform rect ? rect.rect.height : Screen.height;
        panel = KitHudPanel.Create(root, "WildbloodBossBoard", new Vector2(400, 250), new Vector2(48, Mathf.Max(0, height * .5f - 125)), global::ExtraPotions.Wildblood.Runtime.Plugin.Guid, "WildbloodBossBoard");
        if (panel == null) return false;
        hudOwner = root;
        preview = Label(panel.Content, "Damage", new Vector2(14, -10), new Vector2(372, 195), 16);
        open = KitButton.Create(panel.Content, "FullBoard", T("open"), () => { if (Available && selected != null) Replay(selected); },
            new Vector2(14, -206), new Vector2(372, 32));
        if (open != null) panel.SetInteractive(open.GetComponent<Graphic>(), true);
        return true;
    }

    static void EnsureCommand()
    {
        if (!Plugin.AllowsUi || command != null || Terminal.commands.ContainsKey("damage")) return;
        command = new Terminal.ConsoleCommand("damage", T("help"), (Terminal.ConsoleEventArgs args) =>
        {
            if (args.Args.Length != 1) { args.Context?.AddString(T("help")); return; }
            if (!Available) { args.Context?.AddString(T("unavailable")); return; }
            BossBoard? board = BossBoard.Latest;
            if (board != null) { if (!Replay(board)) args.Context?.AddString(T("unavailable")); return; }
            CancelRequest();
            long currentGeneration = CruciblePlugin.Session.Generation;
            var bus = ZRoutedRpc.instance;
            var player = Player.m_localPlayer;
            long profile = player != null ? player.GetPlayerID() : 0;
            int currentEpoch = epoch;
            waitingContext = args.Context; waitingActor = player; waitingProfile = profile; waitingBus = bus;
            pending = answer =>
            {
                if (!Available || epoch != currentEpoch || CruciblePlugin.Session.Generation != currentGeneration || !ReferenceEquals(bus, ZRoutedRpc.instance)
                    || player == null || player != Player.m_localPlayer || player.GetPlayerID() != profile) return;
                pending = null; waitingContext = null; waitingActor = null; waitingBus = null; waitingProfile = 0;
                if (answer != null) Replay(answer);
                else if (args.Context != null) args.Context.AddString(T("none"));
            };
            waitingUntil = Time.unscaledTime + 5f;
            if (!BossBoardRecall.Request(pending)) { CancelRequest(); args.Context?.AddString(T("none")); }
            else args.Context?.AddString(T("waiting"));
        });
    }
    static void CancelRequest()
    {
        if (pending != null) BossBoardRecall.CancelPending(pending);
        pending = null; waitingContext = null; waitingActor = null; waitingBus = null; waitingProfile = 0; epoch++;
    }
    static void ReleasePanel() { panel?.Dispose(); panel = null; hudOwner = null; preview = null; open = null; }
    internal static void Reset()
    {
        CancelRequest(); ReleasePanel();
        if (window != null) { window.Close(); window.Destroy(); window = null; }
        heading = pageText = null; previous = next = null; Array.Clear(names, 0, names.Length); Array.Clear(damage, 0, damage.Length);
        observed = selected = cachedBoard = null; cachedRows = Array.Empty<DamageTally.Entry>(); previewText = ""; drawnPage = language = -1;
        page = 0; hideAt = 0; explicitPreview = false;
    }
    internal static void Shutdown()
    {
        Reset();
        if (command != null && Terminal.commands.TryGetValue("damage", out var current) && ReferenceEquals(current, command)) Terminal.commands.Remove("damage");
        command = null; generation = -1;
    }
}

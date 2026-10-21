// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Saga.Config;
using ExtraPotions.Saga.Powers;
using ExtraPotions.Saga.Runtime.Adopted.Grindstone;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Meadworks.Crucible.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExtraPotions.Saga.Runtime
{
    /// <summary>
    /// Hold the wheel key or gamepad button to pick one attuned power. Cards are Crucible rounded rectangles tinted with
    /// kit color roles, so Timber and Brown restyle them with the rest of the menu. The accent ring marks only the aimed power.
    /// </summary>
    public static class PowerWheelRuntime
    {
        const float CardWidth = 156f;
        const float CardHeight = 52f;
        static readonly PowerWheelSession session = new PowerWheelSession();
        static readonly List<Wedge> wedges = new List<Wedge>();
        static KitHudPanel hud;
        static TMP_Text centerName;
        static TMP_Text centerHint;
        static string layout = "";
        static Vector2 aim;
        static int? stepped;
        static bool toldNone;
        static int claimedFrame = -1;
        static int padDir;
        static int padFrame = -1;
        static Boss[] shownBosses = Array.Empty<Boss>();
        static string centerShown = "";
        static string hintShown = "";

        public static bool CapturesLook { get; private set; }

        sealed class Wedge
        {
            public Boss Boss;
            public Image Ring;
            public Image Fill;
            public TMP_Text Label;
            public bool Activatable;
            public string Shown = "";
        }

        public static void Poll()
        {
            try { Step(); }
            catch (Exception error)
            {
                NativeSaga.Report(error, "power wheel");
                Shutdown();
            }
        }

        public static void Shutdown()
        {
            session.Reset();
            aim = Vector2.zero;
            stepped = null;
            toldNone = false;
            CapturesLook = false;
            claimedFrame = -1;
            padDir = 0;
            padFrame = -1;
            DestroyUi();
        }

        static void Step()
        {
            var player = Player.m_localPlayer;
            var settings = Plugin.Settings;
            if (!Plugin.AllowsUi || player == null || settings == null || Hud.instance == null)
            {
                Shutdown();
                return;
            }
            if (session.IsOpen) claimedFrame = Time.frameCount;
            bool powers = NativeSaga.Active("powers");
            bool all = settings.Get(SagaSettings.Powers.ActivateAllAttuned);
            var bosses = powers ? NativePowers.WheelBosses(player) : Array.Empty<Boss>();
            // The game's radial, not Hud.InRadial. A postfix forces that true while this wheel is open, and reading it here would close the wheel.
            bool gameplay = MeadworksInput.Allows(InputContext.Gameplay) && player.TakeInput() && !player.IsDead()
                && !player.InPlaceMode() && !player.InCutscene() && !player.IsTeleporting() && !GameRadialOpen();
            bool bound = KeyBound(settings) || PadBound(settings);
            bool held = KeyHeld(settings) || PadHeld(settings);
            bool edge = KeyDown(settings) || PadEdge(settings);
            if (edge && powers && !all && gameplay && bosses.Length == 0)
            {
                if (!toldNone) player.Message(MessageHud.MessageType.Center, SagaText.Text("power_wheel_none"));
                toldNone = true;
            }
            else if (!edge) toldNone = false;
            int? aimIndex = null;
            if (session.IsOpen) aimIndex = Aim(bosses.Length);
            bool confirm = session.IsOpen && aimIndex.HasValue && Confirm(settings);
            bool cancel = EscapeDown() || CancelPad(settings);
            var result = session.Tick(PowerWheelRules.Shows(powers, all, bosses.Length, bound) && gameplay, held, cancel, confirm, aimIndex);
            if (result.Signal == PowerWheelSignal.Opened)
            {
                aim = Vector2.zero;
                stepped = null;
            }
            if (result.Signal == PowerWheelSignal.Commit && result.Index is int index && index >= 0 && index < bosses.Length)
                NativePowers.TrySelectWheel(player, bosses[index]);
            CapturesLook = session.IsOpen;
            if (CapturesLook) claimedFrame = Time.frameCount;
            ClaimButtons();
            if (!session.IsOpen) DestroyUi();
            else Present(player, bosses, session.Highlight);
        }

        static bool Blocks() => PowerWheelCapture.BlocksGame(CapturesLook, claimedFrame, Time.frameCount);

        static bool GameRadialOpen()
        {
            var hud = Hud.instance;
            return hud != null && hud.m_radialMenu != null && hud.m_radialMenu.gameObject.activeSelf;
        }

        static bool EscapeDown() => ZInput.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Escape);

        static void ClaimButtons()
        {
            if (!Blocks()) return;
            if (padFrame != Time.frameCount)
            {
                padDir = 0;
                if (MeadworksInput.PadDown(PadButton.DPadRight) || MeadworksInput.PadDown(PadButton.DPadDown)) padDir = 1;
                else if (MeadworksInput.PadDown(PadButton.DPadLeft) || MeadworksInput.PadDown(PadButton.DPadUp)) padDir = -1;
                padFrame = Time.frameCount;
            }
            // D-pad is read above, then consumed. JoyButtonA stays, or the wheel's own confirm would miss the press.
            MeadworksInput.PadConsume(PadButton.DPadUp);
            MeadworksInput.PadConsume(PadButton.DPadDown);
            MeadworksInput.PadConsume(PadButton.DPadLeft);
            MeadworksInput.PadConsume(PadButton.DPadRight);
            foreach (var name in PowerWheelCapture.RoutedButtons)
                ZInput.ResetButtonStatus(name);
        }

        static int? Aim(int count)
        {
            float mx = Input.GetAxisRaw("Mouse X");
            float my = Input.GetAxisRaw("Mouse Y");
            bool mouseMoved = Mathf.Abs(mx) + Mathf.Abs(my) > 0.02f;
            aim += new Vector2(mx, my) * 0.06f;
            if (aim.sqrMagnitude > 1f) aim = aim.normalized;
            // GetJoyRightStickY is already negated inside the game, so stick-up arrives negative. The wheel treats positive Y as up.
            var stick = new Vector2(ZInput.GetJoyRightStickX(), -ZInput.GetJoyRightStickY());
            bool stickAimed = stick.sqrMagnitude >= (float)(PowerWheelAim.Deadzone * PowerWheelAim.Deadzone);
            if (mouseMoved || stickAimed) stepped = null;
            if (stickAimed) aim = stick.sqrMagnitude > 1f ? stick.normalized : stick;
            int dir = 0;
            if (padFrame == Time.frameCount) dir = padDir;
            else if (MeadworksInput.PadDown(PadButton.DPadRight) || MeadworksInput.PadDown(PadButton.DPadDown)) dir = 1;
            else if (MeadworksInput.PadDown(PadButton.DPadLeft) || MeadworksInput.PadDown(PadButton.DPadUp)) dir = -1;
            if (dir != 0) stepped = PowerWheelAim.Step(stepped ?? session.Highlight, count, dir);
            if (stepped.HasValue) return stepped;
            var wedge = PowerWheelAim.Wedge(count, aim.x, aim.y);
            return wedge;
        }

        static bool KeyBound(SagaConfig settings)
        {
            var entry = settings.WheelKey;
            return entry != null && entry.Value.MainKey != KeyCode.None;
        }

        static bool KeyHeld(SagaConfig settings) => KeyBound(settings) && MeadworksInput.Held(settings.WheelKey);
        static bool KeyDown(SagaConfig settings) => KeyBound(settings) && MeadworksInput.Pressed(settings.WheelKey);

        static bool TryPad(SagaConfig settings, out PadButton button)
        {
            button = default;
            var choice = settings.Get(SagaSettings.Powers.PowerWheelGamepad);
            return choice != PowerWheelPad.Unbound && GamepadMap.TryParse(choice.ToString(), out button);
        }

        static bool PadBound(SagaConfig settings) => TryPad(settings, out _);
        static bool PadHeld(SagaConfig settings) => TryPad(settings, out var button) && MeadworksInput.PadHeld(button);
        static bool PadEdge(SagaConfig settings) => TryPad(settings, out var button) && MeadworksInput.PadDown(button);

        static bool Confirm(SagaConfig settings)
        {
            if (!MeadworksInput.PadDown(PadButton.A)) return false;
            return !TryPad(settings, out var button) || button != PadButton.A;
        }

        static bool CancelPad(SagaConfig settings)
        {
            if (!MeadworksInput.PadDown(PadButton.B)) return false;
            return !TryPad(settings, out var button) || button != PadButton.B;
        }

        static void Present(Player player, Boss[] bosses, int? highlight)
        {
            if (hud == null || hud.Root == null || !SameBosses(bosses))
            {
                Build(bosses, string.Join(",", bosses));
                shownBosses = (Boss[])bosses.Clone();
            }
            if (hud == null) return;
            Center();
            hud.Update(true, false, 1f, 0.94f);
            var text = KitAppearance.Color(KitColorRole.Text);
            var muted = KitAppearance.Color(KitColorRole.Muted);
            var shade = KitAppearance.Color(KitColorRole.Shade);
            var accent = KitAppearance.Color(KitColorRole.Accent);
            for (int i = 0; i < wedges.Count; i++)
            {
                var wedge = wedges[i];
                bool on = highlight == i;
                wedge.Ring.color = accent;
                wedge.Ring.enabled = on;
                wedge.Fill.color = shade;
                wedge.Label.color = wedge.Activatable ? text : muted;
                string label = NativePowers.WheelLabel(wedge.Boss);
                if (wedge.Shown != label)
                {
                    wedge.Label.text = label;
                    wedge.Shown = label;
                }
            }
            bool aimed = highlight.HasValue && highlight.Value >= 0 && highlight.Value < wedges.Count;
            var chosen = aimed ? wedges[highlight.Value] : null;
            string name = chosen == null ? SagaText.Text("power_wheel_choose") : NativePowers.WheelLabel(chosen.Boss);
            if (centerShown != name)
            {
                centerName.text = name;
                centerShown = name;
            }
            centerName.color = KitAppearance.Color(KitColorRole.Heading);
            string hint = chosen != null && !chosen.Activatable
                ? SagaText.Format("power_wheel_unavailable", NativePowers.WheelLabel(chosen.Boss))
                : SagaText.Text("power_wheel_release");
            if (hintShown != hint)
            {
                centerHint.text = hint;
                hintShown = hint;
            }
            centerHint.color = muted;
        }

        static bool SameBosses(Boss[] bosses)
        {
            if (shownBosses.Length != bosses.Length) return false;
            for (int i = 0; i < bosses.Length; i++)
                if (shownBosses[i] != bosses[i]) return false;
            return true;
        }

        static void Build(Boss[] bosses, string next)
        {
            DestroyUi();
            layout = next;
            float radius = Mathf.Max(96f, bosses.Length * 26f);
            float panel = (radius + CardWidth * 0.5f + 28f) * 2f;
            hud = KitHudPanel.Create(Hud.instance.transform, "Saga_power_wheel", new Vector2(panel, panel), Vector2.zero, Plugin.Guid, "Saga_power_wheel");
            if (hud == null) return;
            for (int i = 0; i < bosses.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / bosses.Length;
                float x = panel * 0.5f + Mathf.Sin(angle) * radius - CardWidth * 0.5f;
                float y = panel * 0.5f - Mathf.Cos(angle) * radius - CardHeight * 0.5f;
                var root = new GameObject("Saga_wheel_" + bosses[i], typeof(RectTransform));
                root.transform.SetParent(hud.Content, false);
                Place((RectTransform)root.transform, new Vector2(x, y), new Vector2(CardWidth, CardHeight));
                var ring = Round(root.transform, "ring", new Vector2(-4f, -4f), new Vector2(CardWidth + 8f, CardHeight + 8f));
                var fill = Round(root.transform, "fill", Vector2.zero, new Vector2(CardWidth, CardHeight));
                var icon = NativePowers.WheelIcon(bosses[i]);
                float textX = 8f;
                if (icon != null)
                {
                    var iconObject = new GameObject("icon", typeof(RectTransform), typeof(Image));
                    iconObject.transform.SetParent(root.transform, false);
                    var image = iconObject.GetComponent<Image>();
                    image.sprite = icon;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    Place((RectTransform)iconObject.transform, new Vector2(8f, 10f), new Vector2(32f, 32f));
                    textX = 46f;
                }
                var label = KitCanvas.Text(root.transform, "name", "", 15, Color.white, TextAlignmentOptions.MidlineLeft, true);
                Place(label.rectTransform, new Vector2(textX, 6f), new Vector2(CardWidth - textX - 8f, 40f));
                label.overflowMode = TextOverflowModes.Ellipsis;
                wedges.Add(new Wedge { Boss = bosses[i], Ring = ring, Fill = fill, Label = label, Activatable = NativePowers.WheelCanActivate(bosses[i]) });
            }
            centerName = KitCanvas.Text(hud.Content, "choice", "", 22, Color.white, TextAlignmentOptions.Center, true);
            if (KitCanvas.Heading != null) centerName.font = KitCanvas.Heading;
            Place(centerName.rectTransform, new Vector2(panel * 0.5f - 120f, panel * 0.5f - 30f), new Vector2(240f, 28f));
            centerHint = KitCanvas.Text(hud.Content, "hint", "", 14, Color.white, TextAlignmentOptions.Center, true);
            Place(centerHint.rectTransform, new Vector2(panel * 0.5f - 120f, panel * 0.5f + 2f), new Vector2(240f, 40f));
            Center();
        }

        static Image Round(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = RoundedFill.Sprite;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Place((RectTransform)go.transform, at, size);
            return image;
        }

        static void Place(RectTransform rect, Vector2 at, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(at.x, -at.y);
            rect.sizeDelta = size;
        }

        static void Center()
        {
            if (hud?.Root?.parent is RectTransform parent)
            {
                var size = hud.Root.sizeDelta;
                hud.SetPosition(new Vector2((parent.rect.width - size.x) * 0.5f, (parent.rect.height - size.y) * 0.5f));
            }
        }

        static void DestroyUi()
        {
            hud?.Dispose();
            hud = null;
            centerName = null;
            centerHint = null;
            wedges.Clear();
            layout = "";
            shownBosses = Array.Empty<Boss>();
            centerShown = "";
            hintShown = "";
        }

        // SetMouseLook already includes the right stick, so the camera must ignore it while the wheel is aiming.
        [HarmonyPatch(typeof(Player), nameof(Player.SetMouseLook))]
        static class Look
        {
            [HarmonyPriority(Priority.Last)]
            static void Prefix(Player __instance, ref Vector2 mouseLook)
            {
                if (__instance == Player.m_localPlayer && Blocks()) mouseLook = Vector2.zero;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetControls))]
        static class Controls
        {
            static void Prefix(Player __instance, ref bool attack, ref bool attackHold, ref bool secondaryAttack, ref bool secondaryAttackHold, ref bool jump, ref bool dodge)
            {
                if (__instance != Player.m_localPlayer || !Blocks()) return;
                attack = attackHold = secondaryAttack = secondaryAttackHold = jump = dodge = false;
            }
        }

        // Runs before the body, so a D-pad press is stashed and consumed before the guardian power reads it.
        [HarmonyPatch(typeof(Player), "Update")]
        static class Claim
        {
            static void Prefix(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                if (CapturesLook) claimedFrame = Time.frameCount;
                ClaimButtons();
            }
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.InRadial))]
        static class Radial
        {
            [HarmonyPriority(Priority.Last)]
            static void Postfix(ref bool __result)
            {
                if (Blocks()) __result = true;
            }
        }

        // Escape would open the pause menu while its root is hidden. The wheel closes on that same press.
        [HarmonyPatch(typeof(Menu), "Update")]
        static class Pause
        {
            static bool Prefix(Menu __instance)
            {
                if (!Blocks() || __instance == null || __instance.m_root == null || __instance.m_root.gameObject.activeSelf)
                    return true;
                if (!ZInput.GetKeyDown(KeyCode.Escape) && !Input.GetKeyDown(KeyCode.Escape)) return true;
                return false;
            }
        }
    }
}

// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Hold timing adapted from BossRules 1.1.8 ForsakenPowerShortcutInput.
// Copyright (C) 2026 sighsorry. GPL-3.0. Snapshot thunderstore-20261019/sighsorry-BossRules-1.1.8.
// Changed by expDARE, 2026: hold to show, release on a wedge chooses, no native radial.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Saga.Powers
{
    public enum PowerWheelSignal { None, Opened, Commit, Cancel }

    public readonly struct PowerWheelResult
    {
        public PowerWheelResult(PowerWheelSignal signal, int? index)
        {
            Signal = signal;
            Index = index;
        }

        public PowerWheelSignal Signal { get; }
        public int? Index { get; }
    }

    /// <summary>Which attuned powers an activation runs, and when the wheel may open.</summary>
    public static class PowerWheelRules
    {
        public static bool Shows(bool powersActive, bool activateAll, int attuned, bool controlBound) =>
            powersActive && !activateAll && attuned > 0 && controlBound;

        /// <summary><paramref name="newestFirst"/> is the attuned powers that have a game power, newest first. All-together keeps that order. Otherwise only the equipped name, or the newest when it is not attuned.</summary>
        public static IReadOnlyList<string> ActivationOrder(bool activateAll, string? selected, IReadOnlyList<string> newestFirst)
        {
            if (newestFirst == null || newestFirst.Count == 0) return Array.Empty<string>();
            if (activateAll || newestFirst.Count == 1) return Copy(newestFirst);
            if (!string.IsNullOrEmpty(selected))
            {
                for (var i = 0; i < newestFirst.Count; i++)
                    if (string.Equals(newestFirst[i], selected, StringComparison.OrdinalIgnoreCase))
                        return new[] { newestFirst[i] };
            }
            return new[] { newestFirst[0] };
        }

        static string[] Copy(IReadOnlyList<string> names)
        {
            var copy = new string[names.Count];
            for (var i = 0; i < names.Count; i++) copy[i] = names[i];
            return copy;
        }
    }

    /// <summary>Candidates for a wheel key, skipping keys the game or another MeadWorks mod already uses. Empty when every candidate is taken. The shipped default stays unbound.</summary>
    public static class PowerWheelKeys
    {
        public static readonly string[] Candidates = { "F6", "F7", "F9", "F10", "F11" };

        public static string MainKey(string? chord)
        {
            var text = (chord ?? "").Trim();
            if (text.Length == 0 || text.Equals("None", StringComparison.OrdinalIgnoreCase)) return "";
            var split = text.IndexOf('+');
            var main = (split < 0 ? text : text.Substring(0, split)).Trim();
            const string keyboard = "<Keyboard>/";
            if (main.StartsWith(keyboard, StringComparison.OrdinalIgnoreCase)) main = main.Substring(keyboard.Length);
            return main;
        }

        public static string FirstFree(IEnumerable<string>? occupied)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (occupied != null)
                foreach (var entry in occupied)
                {
                    var main = MainKey(entry);
                    if (main.Length > 0) taken.Add(main);
                }
            foreach (var candidate in Candidates)
                if (!taken.Contains(candidate)) return candidate;
            return "";
        }
    }

    /// <summary>Wedge under an aim vector. Index 0 is up, then clockwise. Null inside the deadzone.</summary>
    public static class PowerWheelAim
    {
        public const double Deadzone = 0.35;

        public static int? Wedge(int count, double x, double y, double deadzone = Deadzone)
        {
            if (count <= 0 || deadzone < 0 || double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y)) return null;
            if (x * x + y * y < deadzone * deadzone) return null;
            var angle = Math.Atan2(x, y);
            if (angle < 0) angle += 2 * Math.PI;
            var index = (int)Math.Floor(angle / (2 * Math.PI / count));
            if (index < 0) index = 0;
            if (index >= count) index = count - 1;
            return index;
        }

        public static int Step(int? current, int count, int direction)
        {
            if (count <= 0 || direction == 0) return current ?? 0;
            if (!current.HasValue) return direction > 0 ? 0 : count - 1;
            var next = current.Value + (direction > 0 ? 1 : -1);
            if (next < 0) next = count - 1;
            if (next >= count) next = 0;
            return next;
        }
    }

    /// <summary>Hold opens the wheel without a selection. Release on a wedge commits. Release in the center, cancel, or a blocked moment closes it. The hold must be released before it can open again.</summary>
    public sealed class PowerWheelSession
    {
        enum Phase { Idle, Open, Wait }
        Phase phase;
        int? highlight;

        public bool IsOpen => phase == Phase.Open;
        public int? Highlight => highlight;

        public void Reset()
        {
            phase = Phase.Idle;
            highlight = null;
        }

        public PowerWheelResult Tick(bool allowed, bool held, bool cancel, bool confirm, int? aim)
        {
            if (phase == Phase.Wait)
            {
                if (!held) phase = Phase.Idle;
                return new PowerWheelResult(PowerWheelSignal.None, null);
            }
            if (!allowed)
            {
                var open = phase == Phase.Open;
                highlight = null;
                phase = held ? Phase.Wait : Phase.Idle;
                return new PowerWheelResult(open ? PowerWheelSignal.Cancel : PowerWheelSignal.None, null);
            }
            if (phase == Phase.Idle)
            {
                if (!held) return new PowerWheelResult(PowerWheelSignal.None, null);
                phase = Phase.Open;
                highlight = null;
                return new PowerWheelResult(PowerWheelSignal.Opened, null);
            }
            if (cancel)
            {
                highlight = null;
                phase = held ? Phase.Wait : Phase.Idle;
                return new PowerWheelResult(PowerWheelSignal.Cancel, null);
            }
            highlight = aim;
            if (confirm && aim.HasValue)
            {
                var index = aim.Value;
                highlight = null;
                phase = held ? Phase.Wait : Phase.Idle;
                return new PowerWheelResult(PowerWheelSignal.Commit, index);
            }
            if (held) return new PowerWheelResult(PowerWheelSignal.None, aim);
            var chosen = aim;
            highlight = null;
            phase = Phase.Idle;
            return new PowerWheelResult(chosen.HasValue ? PowerWheelSignal.Commit : PowerWheelSignal.Cancel, chosen);
        }
    }

    /// <summary>
    /// Buttons the wheel borrows from the classic gamepad layout. Use and the build menu share face button A under other
    /// names than the wheel's confirm, and the guardian power reads the D-pad under another name than the step.
    /// </summary>
    public static class PowerWheelCapture
    {
        public static readonly string[] RoutedButtons =
        {
            "JoyUse", "JoyBuildMenu", "JoyGP", "JoyDodge", "JoyJump",
            "JoyHotbarUse", "JoyHotbarLeft", "JoyHotbarRight",
            "JoyCamZoomIn", "JoyCamZoomOut", "JoyMiniMapZoomIn", "JoyMiniMapZoomOut",
        };

        public static readonly string[] DPadButtons = { "JoyDPadUp", "JoyDPadDown", "JoyDPadLeft", "JoyDPadRight" };

        /// <summary>The wheel still owns the frame it closes on, so confirm and cancel are not also Use, dodge, or the pause menu.</summary>
        public static bool BlocksGame(bool open, int claimedFrame, int frame) =>
            open || (claimedFrame >= 0 && claimedFrame == frame);
    }

    /// <summary>One rectangle in the wheel, top-left origin, y growing downward.</summary>
    public readonly struct WheelBox
    {
        public WheelBox(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width < 0d ? 0d : width;
            Height = height < 0d ? 0d : height;
        }

        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public double CenterX => X + Width * 0.5d;
        public double CenterY => Y + Height * 0.5d;

        public bool Hits(WheelBox other, double gap)
        {
            if (gap < 0d) gap = 0d;
            return X < other.X + other.Width + gap && other.X < X + Width + gap
                && Y < other.Y + other.Height + gap && other.Y < Y + Height + gap;
        }
    }

    /// <summary>
    /// Forsaken power cards on a ring around the center hint. The oldest attunement is index 0, at the top, then
    /// clockwise. The square that used to back the ring is not part of this layout.
    /// </summary>
    public static class PowerWheelLayout
    {
        public const double CardWidth = 156d;
        public const double CardHeight = 52d;
        public const double CenterWidth = 240d;
        public const double CenterHeight = 72d;
        public const double Gap = 12d;

        public static void Arrange(int count, out WheelBox panel, WheelBox[] cards, out WheelBox center)
        {
            panel = default;
            center = default;
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (count < 0 || count > cards.Length) throw new ArgumentOutOfRangeException(nameof(count));
            double radius = 0d;
            if (count > 0)
            {
                double low = 0d;
                double high = Math.Max(CardWidth, CardHeight) + CenterWidth + Gap;
                for (int step = 0; step < 24; step++)
                {
                    double mid = (low + high) * 0.5d;
                    if (Clear(count, mid)) high = mid;
                    else low = mid;
                }
                radius = high;
            }
            double reach = radius + Math.Max(CardWidth, CardHeight) * 0.5d + Gap;
            double side = Math.Max(reach * 2d, CenterWidth + Gap * 2d);
            side = Math.Max(side, CenterHeight + Gap * 2d);
            panel = new WheelBox(0d, 0d, side, side);
            center = new WheelBox((side - CenterWidth) * 0.5d, (side - CenterHeight) * 0.5d, CenterWidth, CenterHeight);
            for (int i = 0; i < count; i++)
            {
                double angle = count == 0 ? 0d : (Math.PI * 2d) * i / count;
                double x = panel.CenterX + Math.Sin(angle) * radius - CardWidth * 0.5d;
                double y = panel.CenterY - Math.Cos(angle) * radius - CardHeight * 0.5d;
                cards[i] = new WheelBox(x, y, CardWidth, CardHeight);
            }
        }

        static bool Clear(int count, double radius)
        {
            var placed = new WheelBox[count];
            double reach = radius + Math.Max(CardWidth, CardHeight) * 0.5d + Gap;
            double side = Math.Max(reach * 2d, Math.Max(CenterWidth, CenterHeight) + Gap * 2d);
            var middle = new WheelBox((side - CenterWidth) * 0.5d, (side - CenterHeight) * 0.5d, CenterWidth, CenterHeight);
            for (int i = 0; i < count; i++)
            {
                double angle = (Math.PI * 2d) * i / count;
                double x = side * 0.5d + Math.Sin(angle) * radius - CardWidth * 0.5d;
                double y = side * 0.5d - Math.Cos(angle) * radius - CardHeight * 0.5d;
                var card = new WheelBox(x, y, CardWidth, CardHeight);
                if (card.Hits(middle, Gap)) return false;
                for (int earlier = 0; earlier < i; earlier++)
                    if (card.Hits(placed[earlier], Gap)) return false;
                if (card.X < 0d || card.Y < 0d || card.X + card.Width > side || card.Y + card.Height > side) return false;
                placed[i] = card;
            }
            return true;
        }
    }
}

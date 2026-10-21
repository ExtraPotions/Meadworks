// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Meadworks.Crucible
{
    public enum HudAttachSide { Right, Below, Left, Above }

    public sealed class HudLayoutState
    {
        public bool Placed { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Scale { get; set; }
        public float Opacity { get; set; } = -1;
        public bool Hidden { get; set; }
        public string Target { get; set; } = "";
        public HudAttachSide Side { get; set; }
        public float Gap { get; set; } = 8;
        public HudLayoutState Copy() => (HudLayoutState)MemberwiseClone();
    }

    public sealed class HudLayoutNode
    {
        public string Key { get; }
        public HudLayoutState State { get; }
        public float AutomaticX { get; }
        public float AutomaticY { get; }
        public float Width { get; }
        public float Height { get; }
        public bool Available { get; }
        public HudLayoutNode(string key, HudLayoutState state, float x, float y, float width, float height, bool available = true)
        { Key = key; State = state; AutomaticX = x; AutomaticY = y; Width = width; Height = height; Available = available; }
    }

    public readonly struct HudLayoutPosition
    {
        public float X { get; }
        public float Y { get; }
        public bool Attached { get; }
        public HudLayoutPosition(float x, float y, bool attached) { X = x; Y = y; Attached = attached; }
    }

    public static class HudLayoutRules
    {
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float Clamp(float value, float low, float high) => Math.Max(low, Math.Min(high, value));
        public static bool Valid(HudLayoutState state) => state != null && Finite(state.X) && Finite(state.Y)
            && state.X >= 0 && state.X <= 1 && state.Y >= 0 && state.Y <= 1
            && Finite(state.Scale) && (state.Scale == 0 || state.Scale >= .5f && state.Scale <= 3)
            && Finite(state.Opacity) && (state.Opacity == -1 || state.Opacity >= 0 && state.Opacity <= 1)
            && Finite(state.Gap) && state.Gap >= 0 && state.Gap <= 200
            && Enum.IsDefined(typeof(HudAttachSide), state.Side) && state.Target != null && state.Target.Length <= 300;

        public static string Encode(HudLayoutState state)
        {
            if (!Valid(state)) throw new ArgumentException("Invalid HUD layout.");
            return string.Join("|", "1", state.Placed ? "1" : "0", state.X.ToString("R", Invariant), state.Y.ToString("R", Invariant),
                state.Scale.ToString("R", Invariant), state.Opacity.ToString("R", Invariant), state.Hidden ? "1" : "0",
                Uri.EscapeDataString(state.Target), ((int)state.Side).ToString(Invariant), state.Gap.ToString("R", Invariant));
        }

        public static bool TryDecode(string text, out HudLayoutState state)
        {
            state = new HudLayoutState();
            if (string.IsNullOrEmpty(text)) return true;
            var parts = text.Split('|');
            if (parts.Length != 10 || parts[0] != "1" || (parts[1] != "0" && parts[1] != "1") || (parts[6] != "0" && parts[6] != "1")) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, Invariant, out float x) || !float.TryParse(parts[3], NumberStyles.Float, Invariant, out float y)
                || !float.TryParse(parts[4], NumberStyles.Float, Invariant, out float scale) || !float.TryParse(parts[5], NumberStyles.Float, Invariant, out float opacity)
                || !int.TryParse(parts[8], out int side) || !float.TryParse(parts[9], NumberStyles.Float, Invariant, out float gap)) return false;
            string target;
            try { target = Uri.UnescapeDataString(parts[7]); } catch { return false; }
            var candidate = new HudLayoutState { Placed = parts[1] == "1", X = x, Y = y, Scale = scale, Opacity = opacity, Hidden = parts[6] == "1", Target = target, Side = (HudAttachSide)side, Gap = gap };
            if (!Valid(candidate)) return false;
            state = candidate;
            return true;
        }

        public static bool CanAttach(string own, string target, IReadOnlyDictionary<string, HudLayoutNode> nodes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { own };
            for (int depth = 0; !string.IsNullOrEmpty(target); depth++)
            {
                if (depth >= 32 || !seen.Add(target) || !nodes.TryGetValue(target, out var next)) return false;
                if (!Valid(next.State)) return false;
                target = next.State.Placed ? next.State.Target : "";
            }
            return true;
        }

        public static HudLayoutPosition Resolve(string key, IReadOnlyDictionary<string, HudLayoutNode> nodes, float width, float height)
        {
            if (!nodes.TryGetValue(key, out var node)) throw new ArgumentException("HUD owner is not registered.");
            width = Finite(width) ? Math.Max(0, width) : 0;
            height = Finite(height) ? Math.Max(0, height) : 0;
            Resolve(node, nodes, width, height, new HashSet<string>(StringComparer.Ordinal), out var position);
            return position;
        }

        static bool Resolve(HudLayoutNode node, IReadOnlyDictionary<string, HudLayoutNode> nodes, float width, float height, HashSet<string> path, out HudLayoutPosition position)
        {
            var state = Valid(node.State) ? node.State : new HudLayoutState();
            float x = state.Placed ? state.X * width : node.AutomaticX;
            float y = state.Placed ? state.Y * height : node.AutomaticY;
            bool attached = false, valid = true;
            if (state.Placed && state.Target.Length > 0)
            {
                valid = path.Count < 32 && path.Add(node.Key);
                if (valid)
                {
                    valid = nodes.TryGetValue(state.Target, out var target) && target.Available && (target.State == null || !target.State.Hidden);
                    if (valid && target != null)
                    {
                        valid = Resolve(target, nodes, width, height, path, out var anchor);
                        if (valid)
                        {
                            x = anchor.X; y = anchor.Y;
                            switch (state.Side)
                            {
                                case HudAttachSide.Right: x += target.Width + state.Gap; break;
                                case HudAttachSide.Below: y += target.Height + state.Gap; break;
                                case HudAttachSide.Left: x -= node.Width + state.Gap; break;
                                case HudAttachSide.Above: y -= node.Height + state.Gap; break;
                            }
                            attached = true;
                        }
                    }
                    path.Remove(node.Key);
                }
            }
            if (!Finite(x)) x = 0;
            if (!Finite(y)) y = 0;
            if (state.Placed || state.Scale > 0)
            {
                x = Clamp(x, 0, Math.Max(0, width - Math.Max(0, Finite(node.Width) ? node.Width : 0)));
                y = Clamp(y, 0, Math.Max(0, height - Math.Max(0, Finite(node.Height) ? node.Height : 0)));
            }
            position = new HudLayoutPosition(x, y, attached);
            return valid;
        }
    }
}

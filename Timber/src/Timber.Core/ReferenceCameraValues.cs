// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Approved reference-to-Timber configuration semantics; no source plugin implementation copied.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Timber
{
    public static class ReferenceCameraValues
    {
        /// <summary>The game's own build reach; Timber's Extra Reach is added to it.</summary>
        public const float NativeReach = 5f;
        /// <summary>Defaults matching the reference camera: 100 m reach in all and a 10 m pickup range.</summary>
        public const float DefaultExtraReach = 100f - NativeReach, DefaultPickupRange = 10f;
        static readonly Dictionary<string, string> Direct = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["General.Camera Range Multiplier"] = "Range Multiplier",
            ["General.Camera Terrain Clearance"] = "Terrain Clearance",
            ["General.Resource Pickup Range"] = "Pickup Range",
            ["General.Move With Respect To World"] = "Move With Respect To World",
            ["Controls.Invert Mouse Look Horizontal"] = "Invert Mouse Horizontal",
            ["Controls.Invert Mouse Look Vertical"] = "Invert Mouse Vertical",
            ["Controls.Invert Controller Look Horizontal"] = "Invert Pad Horizontal",
            ["Controls.Invert Controller Look Vertical"] = "Invert Pad Vertical",
            ["Helmet Light.Follow Dvergr Circlet Light"] = "Circlet Light",
            ["Helmet Light.Camera Light Color"] = "Circlet Color",
            ["Helmet Light.Camera Light Shadows"] = "Circlet Shadows",
            ["Hotkeys.Toggle build mode"] = "Toggle Key",
            ["Mist.Demister Follow Camera"] = "Demister Follow Camera",
            ["Mist.Demister Range Multiplier"] = "Demister Range Multiplier",
        };

        public static IReadOnlyDictionary<string, string> Map(IReadOnlyDictionary<string, string> source, float width, float height,
            ICollection<string> notes, ICollection<string> errors)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in Direct)
                if (source.TryGetValue(pair.Key, out string text)) values["Build Camera." + pair.Value] = text;
            Try("General.Distance Can Build From Avatar", text =>
            {
                values["Build Camera.Extra Reach"] = Text(Clamp(Number(text) - NativeReach, 0f, 95f));
                notes.Add("Extra Reach = avatar reach minus native 5 m, clamped to 0–95 m; Timber measures reach from the camera.");
            });
            if (source.ContainsKey("General.Distance Can Build From Workbench"))
                notes.Add("Workbench distance is not imported: Timber's camera area uses the station's real range times Range Multiplier.");
            Try("General.Camera Move Speed Multiplier", text =>
            {
                values["Build Camera.Speed"] = Text(Clamp(10f * Number(text) / 3f, 1f, 100f));
                notes.Add("Camera speed = 10 × reference multiplier / 3, clamped to 1–100 m/s.");
            });
            Try("General.Restriction Mode", text =>
            {
                string mode = text.Trim();
                if (mode != "Off" && mode != "CameraNeedsCoziness" && mode != "CameraPickUpNeedsCoziness") throw new FormatException("Unknown restriction mode");
                int comfort = source.TryGetValue("General.Minimum Comfort Level", out string raw)
                    ? int.Parse(raw, CultureInfo.InvariantCulture) : 1;
                if (comfort < 0 || comfort > 50) throw new FormatException("Comfort must be 0–50");
                values["Build Camera.Entry Needs Resting"] = "false";
                values["Build Camera.Pickup Needs Resting"] = "false";
                values["Build Camera.Entry Min Comfort"] = mode == "CameraNeedsCoziness" ? comfort.ToString(CultureInfo.InvariantCulture) : "0";
                values["Build Camera.Pickup Min Comfort"] = mode == "Off" ? "0" : comfort.ToString(CultureInfo.InvariantCulture);
                notes.Add("Reference restriction mode maps to entry/pickup comfort; Timber checks entry needs only on entry.");
            });
            bool offset = source.ContainsKey("Helmet Light.Camera Light Left/Right Offset") || source.ContainsKey("Helmet Light.Camera Light Up/Down Offset") || source.ContainsKey("Helmet Light.Camera Light Forward/Back Offset");
            if (offset) Try("circlet offset", _ =>
            {
                float x = Read("Helmet Light.Camera Light Left/Right Offset", 0f), y = Read("Helmet Light.Camera Light Up/Down Offset", -0.08f), z = Read("Helmet Light.Camera Light Forward/Back Offset", .65f);
                if (Math.Abs(x) > 5f || Math.Abs(y) > 5f || Math.Abs(z) > 5f) throw new FormatException("Circlet offsets must be -5–5 m");
                values["Build Camera.Circlet Offset"] = Vector(x, y, z);
            }, true);
            bool hud = source.ContainsKey("HUD.Pickup Blocked HUD X") || source.ContainsKey("HUD.Pickup Blocked HUD Y");
            if (hud) Try("pickup HUD position", _ =>
            {
                float x = Read("HUD.Pickup Blocked HUD X", .3f), y = Read("HUD.Pickup Blocked HUD Y", .3f);
                if (x < 0 || x > 1 || y < 0 || y > 1 || width <= 0 || height <= 0) throw new FormatException("Invalid source ratio or HUD size");
                values["Build Camera.Pickup Panel Position"] = "{\"x\":" + Text((x - .5f) * width) + ",\"y\":" + Text((y - 1f) * height) + "}";
                notes.Add("HUD screen ratios converted to top-center offset at " + Text(width) + " × " + Text(height) + " UI units; kit font size retained.");
            }, true);
            foreach (string axis in new[] { "Intensity", "Range" })
                Try("Helmet Light.Camera Light " + axis + " Multiplier", text =>
                {
                    float multiplier = Number(text);
                    if (multiplier == 1f) values["Build Camera.Circlet " + axis] = "0";
                    else errors.Add("Circlet " + axis + " multiplier " + text + " cannot map exactly to an absolute/native value. Choose a Timber absolute value; source is preserved.");
                });
            if (source.TryGetValue("General.Verbose Logging", out string verbose) && Toggle(verbose))
                notes.Add("Verbose logging is not imported. Set Crucible's per-product logging override to timber=Verbose if wanted.");
            return values;

            float Read(string key, float fallback) => source.TryGetValue(key, out string raw) ? Number(raw) : fallback;
            void Try(string key, Action<string> apply, bool always = false)
            {
                if (!source.TryGetValue(key, out string raw) && !always) return;
                try { apply(raw ?? ""); }
                catch (Exception e) when (e is FormatException || e is OverflowException) { errors.Add(key + ": " + e.Message); }
            }
        }
        public static bool Toggle(string value)
        {
            if (string.Equals(value.Trim(), "On", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value.Trim(), "Off", StringComparison.OrdinalIgnoreCase)) return false;
            return bool.Parse(value.Trim());
        }
        static float Number(string value)
        {
            float result = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new FormatException("Nonfinite value");
            return result;
        }
        static string Text(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        static string Vector(float x, float y, float z) => "{\"x\":" + Text(x) + ",\"y\":" + Text(y) + ",\"z\":" + Text(z) + "}";
    }
}

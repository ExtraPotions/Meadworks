// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the catalog's sail tint and hull paint (design 4.3, C10, C11). TheGreatestShips is a behavior
// reference only (README: sail color multiplied in; hull color on the planks, not the mast or rudder; painted bands).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Keel.Catalog;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Paints a Keel ship on a graphical client. Each hull clone gets its own copies of the game's materials (the vanilla
    /// ships are never touched); the copies are changed in place, so loaded ships of that kind change at once. Sail:
    /// color multiplied into the sail's materials. Hull: renderers of the hull's visual states (new, worn, broken) apart
    /// from the sail, mast and rudder; a solid color multiplies the material color, bands paint a copy of the plank
    /// texture. Main thread only.
    /// </summary>
    internal static class KeelPaint
    {
        private sealed class Slot
        {
            public Slot(Material copy, bool sail)
            {
                Copy = copy;
                Sail = sail;
                Color = copy.HasProperty("_Color") ? copy.color : Color.white;
                Texture = copy.HasProperty("_MainTex") ? copy.mainTexture : null;
            }

            public Material Copy { get; }
            public bool Sail { get; }
            public Color Color { get; }
            public Texture? Texture { get; }
            public Texture2D? Generated { get; set; }
        }

        private sealed class State
        {
            public List<Slot> Slots { get; } = new List<Slot>();
            public string Key { get; set; } = "";
        }

        private static readonly Dictionary<KeelShip, State> states = new Dictionary<KeelShip, State>();

        public static void Apply(KeelCatalog.Built b)
        {
            var s = Plugin.Settings;
            if (s == null || b?.Prefab == null) return;
            var sail = s.Get(b.Settings.SailColor);
            var hull = s.Get(b.Settings.HullColor);
            var stripes = s.Get(b.Settings.HullStripes);
            if (!states.TryGetValue(b.Type.Ship, out var state)) states[b.Type.Ship] = state = Prepare(b.Prefab);
            var key = sail + "|" + hull + "|" + stripes;
            if (state.Key == key) return;
            state.Key = key;
            var sailTint = Tint(sail);
            var hullPaint = hull.Length == 0 ? (Color?)null : Tint(hull);
            foreach (var slot in state.Slots)
            {
                if (slot.Sail)
                {
                    if (slot.Copy.HasProperty("_Color")) slot.Copy.color = Multiply(slot.Color, sailTint);
                    continue;
                }
                var texture = slot.Texture;
                var colour = slot.Color;
                if (hullPaint.HasValue && stripes <= 0) colour = Multiply(slot.Color, hullPaint.Value);
                if (slot.Generated != null)
                {
                    UnityEngine.Object.Destroy(slot.Generated);
                    slot.Generated = null;
                }
                if (hullPaint.HasValue && stripes > 0 && slot.Texture != null)
                {
                    slot.Generated = Striped(slot.Texture, hullPaint.Value, stripes);
                    if (slot.Generated != null) texture = slot.Generated;
                }
                if (slot.Copy.HasProperty("_Color")) slot.Copy.color = colour;
                if (slot.Copy.HasProperty("_MainTex")) slot.Copy.mainTexture = texture;
            }
        }

        /// <summary>Forgets the paint state; the prefabs keep their material copies (they still render with them).</summary>
        public static void Dispose() => states.Clear();

        private static State Prepare(GameObject prefab)
        {
            var state = new State();
            var ship = prefab.GetComponent<Ship>();
            var wear = prefab.GetComponent<WearNTear>();
            var sail = ship != null && ship.m_sailObject != null ? ship.m_sailObject.transform : null;
            var mast = ship != null && ship.m_mastObject != null ? ship.m_mastObject.transform : null;
            var rudder = ship != null && ship.m_rudderObject != null ? ship.m_rudderObject.transform : null;
            var hullStates = wear == null ? new Transform[0]
                : new[] { wear.m_new, wear.m_worn, wear.m_broken }.Where(g => g != null).Select(g => g.transform).ToArray();
            var copies = new Dictionary<(Material, bool), Material>();
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
                var t = renderer.transform;
                var isSail = Under(t, sail);
                var isHull = !isSail && !Under(t, mast) && !Under(t, rudder) && (hullStates.Length == 0 || hullStates.Any(h => Under(t, h)));
                if (!isSail && !isHull) continue;
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var original = materials[i];
                    if (original == null) continue;
                    if (!copies.TryGetValue((original, isSail), out var copy))
                    {
                        copy = new Material(original) { name = original.name + " (" + prefab.name + ")" };
                        copies[(original, isSail)] = copy;
                        state.Slots.Add(new Slot(copy, isSail));
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
            return state;
        }

        private static bool Under(Transform t, Transform? root)
        {
            if (root == null) return false;
            for (var x = t; x != null; x = x.parent)
                if (x == root) return true;
            return false;
        }

        private static Color Tint(string hex) =>
            HexColor.TryParse(hex, out var c) ? new Color(c.Red, c.Green, c.Blue, 1f) : Color.white;

        private static Color Multiply(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a);

        /// <summary>A readable copy of the plank texture with the painted bands multiplied in; null when it cannot be read.</summary>
        private static Texture2D? Striped(Texture source, Color paint, int stripes)
        {
            var width = Mathf.Clamp(source.width, 4, 2048);
            var height = Mathf.Clamp(source.height, 4, 2048);
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
                {
                    name = source.name + " (Keel paint)",
                    wrapMode = source.wrapMode,
                    filterMode = source.filterMode,
                    anisoLevel = source.anisoLevel,
                };
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                var pixels = texture.GetPixels32();
                for (var y = 0; y < height; y++)
                {
                    if (!HullPaint.Painted((y + 0.5f) / height, stripes)) continue;
                    for (var x = 0; x < width; x++)
                    {
                        var p = pixels[y * width + x];
                        pixels[y * width + x] = new Color32((byte)(p.r * paint.r), (byte)(p.g * paint.g), (byte)(p.b * paint.b), p.a);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(true, false);
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}

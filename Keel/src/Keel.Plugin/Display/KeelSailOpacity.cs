// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Sail Opacity on the local player's ship (design C13, 4.5; user decision 2026-10-08).
using System;
using System.Collections.Generic;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Display;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Performance;
using ExtraPotions.Meadworks.Crucible;
using UnityEngine;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Draws the sail of the ship the local player is on see-through or hides it (Local settings; other ships and other
    /// players are unchanged). Hidden: the sail's renderers are switched off. Faded: the renderers get transparent copies
    /// of their materials with the opacity as alpha, on a transparent shader found in the game when the sail's own shader
    /// cannot fade; without one a faded sail is hidden below half opacity and normal above (logged once). Everything is
    /// put back when the player leaves the ship or Keel stops; the copies are kept while the player stays aboard. Main thread, graphical clients.
    /// </summary>
    internal static class KeelSailOpacity
    {
        private static readonly string[] TransparentShaders = { "Legacy Shaders/Transparent/Diffuse", "Unlit/Transparent", "Sprites/Default" };

        private sealed class Part
        {
            public Part(Renderer renderer)
            {
                Renderer = renderer;
                Enabled = renderer.enabled;
                Originals = renderer.sharedMaterials;
            }

            public Renderer Renderer { get; }
            public bool Enabled { get; }
            public Material[] Originals { get; }
            public Material[]? Faded { get; set; }
        }

        private static Ship? ship;
        private static readonly List<Part> parts = new List<Part>();
        private static Shader? fallback;
        private static bool searched;
        private static bool warned;
        // Materials and colors are written when the look or alpha changes, not on every frame; a hidden sail keeps its
        // renderers switched off every frame as before.
        private static readonly ChangeGate<(SailLook, float)> applied = new ChangeGate<(SailLook, float)>();

        public static void Update(MeadworksLog? log)
        {
            var settings = Plugin.Settings;
            var player = Player.m_localPlayer;
            var current = player != null ? Ship.GetLocalShip() : null;
            var camera = Utils.GetMainCamera();
            if (settings == null || current == null || camera == null || current.m_sailObject == null || !Plugin.AllowsUi
                || !KeelModules.Active(KeelModule.Catalog))
            {
                Restore();
                return;
            }
            var sail = current.m_sailObject.transform;
            var offset = Vector3.Dot(camera.transform.position - sail.position, current.transform.forward);
            var alpha = SailOpacity.Alpha(settings.Get(KeelSettings.Display.SailOpacity), player!.GetControlledShip() == current, true,
                settings.Get(KeelSettings.Display.SailOpacityOnDeck), settings.Get(KeelSettings.Display.SailOpacityBothSides), SailOpacity.Astern(offset));
            var look = SailOpacity.Look(alpha);
            if (current != ship) Restore();
            if (look == SailLook.Normal && ship == null) return;
            if (ship == null)
            {
                ship = current;
                foreach (var renderer in SailRenderers(current)) parts.Add(new Part(renderer));
            }
            if (look == SailLook.Faded && Fallback() == null)
            {
                if (!warned) log?.Info("Keel Sail Opacity: no transparent shader found; the sail is hidden below half opacity instead.");
                warned = true;
                look = alpha < 0.5f ? SailLook.Hidden : SailLook.Normal;
            }
            if (!applied.Changed((look, alpha)) && look != SailLook.Hidden) return;
            foreach (var part in parts)
            {
                if (part.Renderer == null) continue;
                if (look == SailLook.Hidden)
                {
                    part.Renderer.enabled = false;
                    continue;
                }
                part.Renderer.enabled = part.Enabled;
                if (look == SailLook.Normal)
                {
                    part.Renderer.sharedMaterials = part.Originals;
                    continue;
                }
                if (part.Faded == null) part.Faded = Fade(part.Originals);
                foreach (var material in part.Faded)
                    if (material != null && material.HasProperty("_Color"))
                    {
                        var colour = material.color;
                        colour.a = alpha;
                        material.color = colour;
                    }
                part.Renderer.sharedMaterials = part.Faded;
            }
        }

        public static void Restore()
        {
            foreach (var part in parts)
            {
                if (part.Renderer != null)
                {
                    part.Renderer.sharedMaterials = part.Originals;
                    part.Renderer.enabled = part.Enabled;
                }
                if (part.Faded != null)
                    foreach (var material in part.Faded)
                        if (material != null) UnityEngine.Object.Destroy(material);
            }
            parts.Clear();
            ship = null;
            applied.Reset();
        }

        /// <summary>
        /// The sail's renderers. Ship.m_sailObject (ship/visual/Mast/Sail on the vanilla hulls) holds only the sail's
        /// bones; the drawn sail is the cloth mesh beside it (ship/visual/Mast/Karve_Sail/Karve_Sail on the Karve, the
        /// matching mesh on the Longship). In order: the renderers the ship's sail cloth (Ship.m_sailCloth, MagicaCloth2)
        /// simulates, renderers under m_sailObject, then the skinned meshes under the mast (the mast itself is a rigid
        /// mesh). Read by reflection so Keel needs no reference to the cloth assembly. Every Keel hull is a clone of a
        /// vanilla hull, so the same rules find its sail.
        /// </summary>
        internal static List<Renderer> SailRenderers(Ship ship)
        {
            var found = new List<Renderer>();
            void Add(Renderer? renderer)
            {
                if (renderer != null && !(renderer is ParticleSystemRenderer) && !found.Contains(renderer)) found.Add(renderer);
            }
            foreach (var renderer in ClothRenderers(ship)) Add(renderer);
            if (ship.m_sailObject != null)
                foreach (var renderer in ship.m_sailObject.GetComponentsInChildren<Renderer>(true)) Add(renderer);
            if (found.Count == 0 && ship.m_mastObject != null)
                foreach (var renderer in ship.m_mastObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(renderer);
            return found;
        }

        private static IEnumerable<Renderer> ClothRenderers(Ship ship)
        {
            var result = new List<Renderer>();
            try
            {
                if (!(ClothField?.GetValue(ship) is Component cloth) || cloth == null) return result;
                var data = cloth.GetType().GetProperty("SerializeData")?.GetValue(cloth, null);
                if (data?.GetType().GetField("sourceRenderers")?.GetValue(data) is System.Collections.IEnumerable sources)
                    foreach (var source in sources)
                        if (source is Renderer renderer && renderer != null) result.Add(renderer);
                if (result.Count == 0)
                    foreach (var renderer in cloth.GetComponentsInChildren<SkinnedMeshRenderer>(true)) result.Add(renderer);
            }
            catch (Exception) { result.Clear(); }
            return result;
        }

        private static readonly System.Reflection.FieldInfo? ClothField = typeof(Ship).GetField("m_sailCloth");

        private static Material[] Fade(Material[] originals)
        {
            var copies = new Material[originals.Length];
            for (var i = 0; i < originals.Length; i++)
            {
                var original = originals[i];
                if (original == null) continue;
                var copy = new Material(original) { name = original.name + " (Keel sail opacity)" };
                var texture = original.HasProperty("_MainTex") ? original.mainTexture : null;
                var colour = original.HasProperty("_Color") ? original.color : Color.white;
                copy.shader = Fallback()!;
                if (copy.HasProperty("_MainTex")) copy.mainTexture = texture;
                if (copy.HasProperty("_Color")) copy.color = colour;
                copy.renderQueue = 3000;
                copies[i] = copy;
            }
            return copies;
        }

        private static Shader? Fallback()
        {
            if (searched) return fallback;
            searched = true;
            foreach (var name in TransparentShaders)
            {
                try { fallback = Shader.Find(name); }
                catch (Exception) { fallback = null; }
                if (fallback != null) break;
            }
            return fallback;
        }
    }
}

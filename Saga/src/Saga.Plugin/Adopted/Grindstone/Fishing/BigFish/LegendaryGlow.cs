// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Fishing/BigFish/LegendaryGlow.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The glow of a legendary fish, drawn locally on every client with a screen (<see cref="FishMark"/>): a plain object
    /// under the fish with no network view. A pale sea-green point light that pulses slowly, so the water around the fish
    /// shines at night, and a soft halo of the same colour facing the camera (the seam glow's halo, <see cref="SeamLook"/>),
    /// so it reads in daylight too. The halo keeps the same world size whatever the fish's scale.
    /// </summary>
    public sealed class LegendaryGlow : MonoBehaviour
    {
        private const float PulsePerSecond = 0.5f;
        private const float LightRange = 6f;
        private const float LightIntensity = 2.2f;
        private const float HaloSize = 2.2f;

        private static readonly Color SeaGreen = new Color(0.55f, 1f, 0.85f);

        private Light shine;
        private Renderer halo;
        private MaterialPropertyBlock block;

        public static LegendaryGlow Attach(Transform fish)
        {
            GameObject holder = new GameObject("saga.legendary_glow");
            holder.transform.SetParent(fish, false);
            LegendaryGlow glow = holder.AddComponent<LegendaryGlow>();
            glow.block = new MaterialPropertyBlock();
            glow.shine = AddLight(holder.transform);
            glow.halo = SeamLook.Sprite(holder.transform, SeamLook.Halo, "halo");
            return glow;
        }

        public void Remove() => Destroy(gameObject);

        private Action step;

        private void LateUpdate() => HookGuard.Run("legendary glow", step ??= Step);

        private static Light AddLight(Transform parent)
        {
            Light light = parent.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = SeaGreen;
            light.range = LightRange;
            light.shadows = LightShadows.None;
            return light;
        }

        private void Step()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * PulsePerSecond * 2f * Mathf.PI);
            shine.intensity = LightIntensity * (0.6f + 0.4f * pulse);
            Camera camera = Utils.GetMainCamera();
            if (halo == null || camera == null)
                return;
            Transform part = halo.transform;
            part.rotation = camera.transform.rotation;
            float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);
            float size = HaloSize * (0.85f + 0.3f * pulse) / parentScale;
            part.localScale = new Vector3(size, size, size);
            halo.GetPropertyBlock(block);
            Color colour = SeaGreen;
            colour.a = 0.35f + 0.2f * pulse;
            block.SetColor(SeamLook.ColorId, colour);
            halo.SetPropertyBlock(block);
        }
    }
}

// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Seams/SeamGlow.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using System;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The mark on an open seam, drawn on the miner's own client only: a plain local object with no network view, so
    /// nobody else ever sees it. It lives from the moment the seam opens until <see cref="OpenSeams"/> closes it.
    /// <list type="bullet">
    /// <item>A warm gold point light just off the chunk's face, no shadows: in a dark cave or at night the seam pools
    /// light on the stone around it.</item>
    /// <item>A soft gold halo with the game's own glint star in front (<see cref="SeamLook"/>), facing the camera and
    /// unlit, so it reads on grey stone in daylight as well.</item>
    /// </list>
    /// Every frame it sits where a line from the camera to the chunk's centre meets the chunk, so the face the miner looks
    /// at carries it, with the sprites a little towards the camera so the stone does not cut them. It pulses gently,
    /// fades in over a moment and fades out over the window's last <see cref="FadeOutSeconds"/>: the seam is closing.
    /// It hangs under <see cref="SeamDriver.Holder"/>, so the game scene closing takes it along.
    /// </summary>
    internal sealed class SeamGlow : MonoBehaviour
    {
        private const float PulsePerSecond = 0.9f;
        private const float TurnPerSecond = 40f;
        private const float FadeInSeconds = 0.15f;
        private const float FadeOutSeconds = 0.75f;
        private const float LightRange = 2.5f;
        private const float LightIntensity = 1.6f;
        private const float LightOffFace = 0.45f;
        private const float SpriteOffFace = 0.3f;

        private static readonly Color Gold = new Color(1f, 0.8f, 0.42f);
        private static readonly Color PaleGold = new Color(1f, 0.93f, 0.7f);

        private Rock rock;
        private int area;
        private float opened;
        private float closes;
        private float size;
        private Light shine;
        private Renderer halo;
        private Renderer star;
        private MaterialPropertyBlock block;

        /// <summary>
        /// Marks chunk <paramref name="area"/> of <paramref name="rock"/> until Time.time reaches <paramref name="closes"/>,
        /// on the miner's own client when a seam opens (<see cref="OpenSeams.Open"/>).
        /// </summary>
        public static SeamGlow Show(Rock rock, int area, float closes, Transform parent)
        {
            GameObject holder = new GameObject("saga.seam_glow");
            holder.transform.SetParent(parent, false);
            SeamGlow glow = holder.AddComponent<SeamGlow>();
            glow.rock = rock;
            glow.area = area;
            glow.opened = Time.time;
            glow.closes = closes;
            glow.Build();
            glow.Step();
            return glow;
        }

        public void Remove() => Destroy(gameObject);

        private Action step;

        private void LateUpdate() => HookGuard.Run("seam glow", step ??= Step);

        /// <summary>Sizes the mark by the chunk (a boulder's big chunk gets a bigger one) and makes its parts.</summary>
        private void Build()
        {
            Collider collider = RockChunks.ColliderOf(rock, area);
            size = collider != null ? Mathf.Clamp(collider.bounds.extents.magnitude * 0.55f, 0.4f, 0.9f) : 0.55f;
            block = new MaterialPropertyBlock();
            shine = AddLight(transform);
            halo = SeamLook.Sprite(transform, SeamLook.Halo, "halo");
            star = SeamLook.Sprite(transform, SeamLook.Star, "star");
        }

        private static Light AddLight(Transform parent)
        {
            GameObject holder = new GameObject("light");
            holder.transform.SetParent(parent, false);
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Gold;
            light.range = LightRange;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            return light;
        }

        private void Step()
        {
            Collider collider = rock != null && rock.IsValid ? RockChunks.ColliderOf(rock, area) : null;
            Camera camera = Utils.GetMainCamera();
            if (collider == null || camera == null)
                return;
            Vector3 eye = camera.transform.position;
            Vector3 face = FacePoint(collider, eye);
            Vector3 outward = (eye - face).normalized;
            float age = Time.time - opened;
            float pulse = 0.5f + 0.5f * Mathf.Sin(age * PulsePerSecond * 2f * Mathf.PI);
            float fade = Mathf.Clamp01(age / FadeInSeconds) * Mathf.Clamp01((closes - Time.time) / FadeOutSeconds);
            shine.transform.position = face + outward * LightOffFace;
            shine.intensity = LightIntensity * fade * (0.55f + 0.45f * pulse);
            Vector3 front = face + outward * SpriteOffFace;
            Quaternion facing = camera.transform.rotation;
            Color haloColour = Alpha(Gold, 0.38f * fade * (0.65f + 0.35f * pulse));
            Place(halo, front, facing, size * (0.9f + 0.2f * pulse), haloColour);
            Quaternion turned = facing * Quaternion.Euler(0f, 0f, age * TurnPerSecond);
            Place(star, front + outward * 0.02f, turned, size * 1.4f * (0.8f + 0.35f * pulse), Alpha(PaleGold, 0.75f * fade));
        }

        /// <summary>Where the line from the camera to the chunk's centre enters the chunk; the centre when it misses.</summary>
        private static Vector3 FacePoint(Collider collider, Vector3 eye)
        {
            Vector3 centre = collider.bounds.center;
            Vector3 toCentre = centre - eye;
            float distance = toCentre.magnitude;
            if (distance < 0.01f)
                return centre;
            return collider.Raycast(new Ray(eye, toCentre / distance), out RaycastHit hit, distance) ? hit.point : centre;
        }

        private void Place(Renderer sprite, Vector3 position, Quaternion rotation, float scale, Color colour)
        {
            if (sprite == null)
                return;
            Transform part = sprite.transform;
            part.SetPositionAndRotation(position, rotation);
            part.localScale = new Vector3(scale, scale, scale);
            sprite.GetPropertyBlock(block);
            block.SetColor(SeamLook.ColorId, colour);
            sprite.SetPropertyBlock(block);
        }

        private static Color Alpha(Color colour, float alpha)
        {
            colour.a = Mathf.Clamp01(alpha);
            return colour;
        }
    }
}

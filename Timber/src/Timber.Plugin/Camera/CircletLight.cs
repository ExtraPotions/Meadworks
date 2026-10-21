#nullable disable
// OpenKeep BuildCamera. Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Adopted from ValheimMods ad278ecf323901c3645eb1f9f6fac6833a0f8cb1 / OpenKeep/OpenKeep/src/BuildCamera/CircletLight.cs.
// Changed by expDARE, 2026: Timber namespace, Crucible authority/input/localization/lifecycle; kit pickup UI deferred.
// Cartur portions: GPL-3.0-only; f81977b2553d48a2a79eea62969f86bb9d9fa32e. See docs/source-provenance.json.
using UnityEngine;

namespace Timber.Camera
{
    /// <summary>
    /// A light worn on the head shines from the camera too while it is out (Circlet Light): a light of our own on the
    /// game camera, pointing where it looks, given the worn light's settings every frame (so a light another mod
    /// recolours, dims or switches off at runtime follows), except where Circlet Intensity, Range or Spot Angle set
    /// their own. Off with the worn light, with the camera back, or with Circlet Light off. Local only: never
    /// networked; the circlet on the player stays lit as before.
    /// </summary>
    public static class CircletLight
    {
        private static Light copy;

        public static void Update(GameCamera camera)
        {
            Light source = CameraState.Active && CameraPrefs.CircletLight.Value ? CircletSource.Find(Player.m_localPlayer) : null;
            if (source == null)
            {
                if (copy != null)
                    copy.enabled = false;
                return;
            }
            Light light = Get(camera);
            Copy(light, source);
            light.enabled = true;
        }

        public static void Reset()
        {
            if (copy != null) Object.Destroy(copy.gameObject);
            copy = null;
        }

        private static Light Get(GameCamera camera)
        {
            if (copy != null && copy.transform.parent == camera.transform)
                return copy;
            Reset();
            GameObject holder = new GameObject("Timber_CameraCirclet");
            holder.transform.SetParent(camera.transform, worldPositionStays: false);
            copy = holder.AddComponent<Light>();
            return copy;
        }

        private static void Copy(Light light, Light source)
        {
            light.type = source.type;
            light.color = CircletSettings.TryGetConfiguredColor(out Color colour) ? colour : source.color;
            light.colorTemperature = source.colorTemperature;
            light.useColorTemperature = source.useColorTemperature && !CircletSettings.TryGetConfiguredColor(out _);
            Vector3 offset = CameraPrefs.CircletOffset.Value;
            light.transform.localPosition = new Vector3(Mathf.Clamp(offset.x, -5f, 5f), Mathf.Clamp(offset.y, -5f, 5f), Mathf.Clamp(offset.z, -5f, 5f));
            light.cookie = source.cookie;
            light.renderMode = source.renderMode;
            light.cullingMask = source.cullingMask;
            light.shadows = CircletSettings.GetConfiguredShadows(source.shadows);
            light.shadowStrength = source.shadowStrength;
            light.intensity = Own(CameraPrefs.CircletIntensity.Value, source.intensity);
            light.range = Own(CameraPrefs.CircletRange.Value, CircletSource.BaseRange(source));
            light.spotAngle = Own(CameraPrefs.CircletSpotAngle.Value, source.spotAngle);
            light.innerSpotAngle = Mathf.Min(source.innerSpotAngle, light.spotAngle);
        }

        /// <summary>The setting when it is above 0, else the worn light's own value.</summary>
        private static float Own(float setting, float own) => setting > 0f ? setting : own;
    }
}

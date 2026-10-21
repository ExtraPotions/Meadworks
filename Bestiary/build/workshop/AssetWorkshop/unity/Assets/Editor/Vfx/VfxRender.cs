using System;
using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>How a system draws: render mode, sorting, size limits, stretching, mesh, material and the vertex streams
    /// the game's shader for it reads.</summary>
    public static class VfxRender
    {
        public static void Apply(ParticleSystemRenderer r, RenderSpec s, Func<string, Material> materials, Func<string, Mesh> meshes)
        {
            r.renderMode = Mode(s.mode);
            r.sortMode = Sort(s.sort);
            r.sortingFudge = s.fudge;
            r.sortingOrder = s.order;
            r.minParticleSize = s.min_size;
            r.maxParticleSize = s.max_size;
            r.lengthScale = s.length_scale;
            r.velocityScale = s.speed_scale;
            r.cameraVelocityScale = s.camera_speed_scale;
            r.alignment = Alignment(s.alignment);
            r.pivot = VfxCurves.Vector(s.pivot);
            r.flip = VfxCurves.Vector(s.flip);
            r.shadowCastingMode = s.shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            KeepColours(r);
            r.sharedMaterial = string.IsNullOrEmpty(s.material) ? null : materials(s.material);
            r.trailMaterial = string.IsNullOrEmpty(s.trail_material) ? null : materials(s.trail_material);
            if (r.renderMode == ParticleSystemRenderMode.Mesh && !string.IsNullOrEmpty(s.mesh))
                r.mesh = meshes(s.mesh);
            Streams(r, s.streams);
        }

        /// <summary>The game's renderers leave particle colours unconverted (m_ApplyActiveColorSpace 0); new ones in
        /// Unity 6 would convert them to linear, so the flag is cleared through the serialized object.</summary>
        private static void KeepColours(ParticleSystemRenderer r)
        {
            var serialized = new UnityEditor.SerializedObject(r);
            var flag = serialized.FindProperty("m_ApplyActiveColorSpace");
            if (flag == null)
                return;
            flag.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Streams(ParticleSystemRenderer r, string[] names)
        {
            if (names == null || names.Length == 0)
                return;
            var streams = new List<ParticleSystemVertexStream>();
            foreach (string name in names)
                streams.Add((ParticleSystemVertexStream)Enum.Parse(typeof(ParticleSystemVertexStream), name));
            r.SetActiveVertexStreams(streams);
        }

        public static ParticleSystemRenderMode Mode(string mode)
        {
            switch (mode)
            {
                case "stretched": return ParticleSystemRenderMode.Stretch;
                case "horizontal": return ParticleSystemRenderMode.HorizontalBillboard;
                case "vertical": return ParticleSystemRenderMode.VerticalBillboard;
                case "mesh": return ParticleSystemRenderMode.Mesh;
                case "none": return ParticleSystemRenderMode.None;
                default: return ParticleSystemRenderMode.Billboard;
            }
        }

        private static ParticleSystemSortMode Sort(string sort) =>
            sort == "distance" ? ParticleSystemSortMode.Distance
            : sort == "oldest_in_front" ? ParticleSystemSortMode.OldestInFront
            : sort == "youngest_in_front" ? ParticleSystemSortMode.YoungestInFront : ParticleSystemSortMode.None;

        private static ParticleSystemRenderSpace Alignment(string alignment) =>
            alignment == "world" ? ParticleSystemRenderSpace.World
            : alignment == "local" ? ParticleSystemRenderSpace.Local
            : alignment == "facing" ? ParticleSystemRenderSpace.Facing
            : alignment == "velocity" ? ParticleSystemRenderSpace.Velocity : ParticleSystemRenderSpace.View;
    }
}

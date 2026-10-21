// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// Builds one Nightfall tornado, made entirely in code: its funnel, two cones of churning cloud one inside the other
    /// (<see cref="TornadoCone"/>), a particle system for each layer round it (<see cref="TornadoLayer"/>), each only
    /// emitting and drawing while <see cref="TornadoSwirl"/> moves its particles,
    /// a cold light in its foot that glows faintly and flares with the lightning inside it, and its wind. It is built switched
    /// off, so nothing plays before it is first raised, and is kept and reused (<see cref="TornadoPool"/>).
    /// </summary>
    internal static class TornadoParts
    {
        /// <summary>Back to front: each is biased to draw behind the ones after it, so the funnel's cloud shows over
        /// the debris cloud and the grit over both.</summary>
        private static readonly TornadoLayer[] Layers =
            { TornadoLayer.Skirt, TornadoLayer.Sheath, TornadoLayer.Funnel, TornadoLayer.Grit };

        /// <summary>The sorting bias between one layer and the next.</summary>
        private const float SortStep = 10f;

        private const float LightHeight = 2.5f;
        private const float LightRange = 12f;

        private static readonly Color LightColour = new Color(0.62f, 0.72f, 1f);

        public static TornadoVisual Build()
        {
            GameObject root = new GameObject("ecr_nightfall_tornado");
            root.SetActive(false);
            TornadoSwirl swirl = new TornadoSwirl();
            for (int i = 0; i < Layers.Length; i++)
            {
                swirl.Add(System(root.transform, Layers[i], (Layers.Length - 1 - i) * SortStep), Layers[i]);
            }
            TornadoVisual tornado = root.AddComponent<TornadoVisual>();
            Material? cloud = TornadoLooks.Cloud();
            TornadoCone[] cones =
                { new TornadoCone(root.transform, TornadoCone.Core, cloud), new TornadoCone(root.transform, TornadoCone.Veil, cloud) };
            tornado.Build(swirl, cones, Glow(root.transform), TornadoLooks.Wind(root.transform));
            return tornado;
        }

        private static ParticleSystem System(Transform root, TornadoLayer layer, float behind)
        {
            GameObject holder = new GameObject("ecr_tornado_" + layer.Name);
            holder.transform.SetParent(root, false);
            ParticleSystem system = holder.AddComponent<ParticleSystem>();
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            Main(system, layer);
            Feed(system);
            Render(system, layer, behind);
            return system;
        }

        // No speed or gravity of its own: the swirl places every particle each frame, in the tornado's own space.
        private static void Main(ParticleSystem system, TornadoLayer layer)
        {
            ParticleSystem.MainModule main = system.main;
            main.duration = 5f;
            main.loop = true;
            main.prewarm = !layer.Kicked;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(layer.Lifetime * 0.85f, layer.Lifetime * 1.15f);
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = layer.MaxParticles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        }

        // Born in a small ball round the foot, which gives each its own angle round the axis; the rate is set as it runs.
        private static void Feed(ParticleSystem system)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            shape.radiusThickness = 1f;
        }

        private static void Render(ParticleSystem system, TornadoLayer layer, float behind)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = layer.Specks ? TornadoLooks.Specks() : TornadoLooks.Smoke();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 1f;
            renderer.sortingFudge = behind;
            if (!layer.Specks)
            {
                Flipbook(system);
            }
        }

        // Each puff holds one wisp of the sheet, picked at random, so no two look alike.
        private static void Flipbook(ParticleSystem system)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = TornadoTextures.WispTiles;
            sheet.numTilesY = TornadoTextures.WispTiles;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;
        }

        private static Light Glow(Transform root)
        {
            GameObject holder = new GameObject("ecr_tornado_light");
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = Vector3.up * LightHeight;
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LightColour;
            light.range = LightRange;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}

// Portions Copyright (C) 2026 Gerald Glasgow. ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: private Bestiary namespace.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.LocalEffects
{
    /// <summary>
    /// Resizes a local copy by a factor, part by part, so the whole effect takes the new size whatever its prefab was
    /// authored with. Scaling the copy's transform is not enough: a particle system in "Local" scaling mode ignores its
    /// parents' scale, and a "Shape" one only moves its emitter. So each particle system's own numbers are multiplied -
    /// start size, start speed, gravity, the velocity, force, limit and noise modules, the emitter shape and its light
    /// module's reach - which works the same in every scaling mode and simulation space. Plain lights reach as far times
    /// the factor (<see cref="ScaleLights"/>), the offsets between the copy's parts shrink with it, a plain mesh with
    /// nothing under it takes the factor as its scale, and a camera shake it carries reaches as much less far, as much
    /// more gently. No transform above a particle system is scaled, so nothing is resized twice.
    /// </summary>
    internal static class ScaleParts
    {
        public static void Apply(GameObject clone, float factor)
        {
            foreach (ParticleSystem system in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                Size(system, factor);
                Speed(system, factor);
                Forces(system, factor);
                Shape(system, factor);
            }
            ScaleLights.Apply(clone, factor);
            Meshes(clone, factor);
            Offsets(clone.transform, factor);
            Shakes(clone, factor);
        }

        // Particle size, per axis when the system sizes its particles in three; the light module's reach with it. A trail
        // is as wide as its particle unless it says otherwise, so only a trail with a width of its own is resized here.
        private static void Size(ParticleSystem system, float f)
        {
            ParticleSystem.MainModule main = system.main;
            if (main.startSize3D)
            {
                main.startSizeX = Times(main.startSizeX, f);
                main.startSizeY = Times(main.startSizeY, f);
                main.startSizeZ = Times(main.startSizeZ, f);
            }
            else
            {
                main.startSize = Times(main.startSize, f);
            }
            ParticleSystem.LightsModule lights = system.lights;
            if (!lights.sizeAffectsRange)
            {
                lights.rangeMultiplier *= f; // otherwise the particle's own size, already scaled, sets the reach
            }
            ParticleSystem.TrailModule trails = system.trails;
            if (!trails.sizeAffectsWidth)
            {
                trails.widthOverTrailMultiplier *= f;
            }
        }

        // How far a particle travels: its launch speed and its fall both shrink, so a spray keeps its arc at the new size.
        private static void Speed(ParticleSystem system, float f)
        {
            ParticleSystem.MainModule main = system.main;
            main.startSpeed = Times(main.startSpeed, f);
            main.gravityModifier = Times(main.gravityModifier, f);
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.x = Times(velocity.x, f);
            velocity.y = Times(velocity.y, f);
            velocity.z = Times(velocity.z, f);
            velocity.radial = Times(velocity.radial, f);
        }

        private static void Forces(ParticleSystem system, float f)
        {
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.x = Times(force.x, f);
            force.y = Times(force.y, f);
            force.z = Times(force.z, f);
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.limit = Times(limit.limit, f);
            limit.limitX = Times(limit.limitX, f);
            limit.limitY = Times(limit.limitY, f);
            limit.limitZ = Times(limit.limitZ, f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.strengthX = Times(noise.strengthX, f); // strengthX is the whole strength when the axes are not split
            noise.strengthY = Times(noise.strengthY, f);
            noise.strengthZ = Times(noise.strengthZ, f);
        }

        // Where particles are born: the emitter shape's own transform, which every shape type is drawn through - a
        // sphere's or cone's radius, a cone's length, a box's size (its scale) - so its scale and offset alone resize any
        // of them. Scaling the radius as well would shrink a sphere twice.
        private static void Shape(ParticleSystem system, float f)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.scale *= f;
            shape.position *= f;
        }

        // A mesh drawn outside the particle systems (a flash card, a chunk of debris) shrinks by its own scale - only a
        // leaf, so no part hanging under it is resized twice.
        private static void Meshes(GameObject clone, float f)
        {
            foreach (MeshRenderer mesh in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                Transform part = mesh.transform;
                if (part.childCount == 0 && part.GetComponent<ParticleSystem>() == null)
                {
                    part.localScale *= f;
                }
            }
        }

        // A camera shake the effect carries is felt from as much less far, and as much more gently: a burst a fifth of
        // the size should not shake the screen of everyone within 50 metres. It fires from its Start, which comes after
        // the copy is made and resized, so this lands in time.
        private static void Shakes(GameObject clone, float f)
        {
            foreach (CamShaker shaker in clone.GetComponentsInChildren<CamShaker>(true))
            {
                shaker.m_range *= f;
                shaker.m_strength *= f;
            }
        }

        // The parts sit closer together by the same factor; the root stays where the effect was placed.
        private static void Offsets(Transform root, float f)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            {
                if (part != root)
                {
                    part.localPosition *= f;
                }
            }
        }

        /// <summary>
        /// A curve or constant times a factor, in every mode. Each mode is scaled through its own values: the module-level
        /// "multiplier" properties scale only the upper constant of a random-between-two-constants value.
        /// </summary>
        private static ParticleSystem.MinMaxCurve Times(ParticleSystem.MinMaxCurve value, float f)
        {
            switch (value.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    value.constant *= f;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    value.constantMin *= f;
                    value.constantMax *= f;
                    break;
                default:
                    value.curveMultiplier *= f; // Curve and TwoCurves share one multiplier
                    break;
            }
            return value;
        }
    }
}

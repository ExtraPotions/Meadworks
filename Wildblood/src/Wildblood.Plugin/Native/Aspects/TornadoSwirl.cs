// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What moves a Nightfall tornado's particles. Each layer's particle system only emits and draws; every frame this
    /// lays each particle on its layer's path itself (<see cref="TornadoLayer"/>): it climbs from the foot to the top
    /// of its layer over its life, turns round the axis faster where the funnel is narrow, keeps to the layer's radius
    /// at its height, and grows as it climbs, fading in at the foot and out near the top. The axis bends: its top
    /// sways a little all the time and trails behind as the tornado moves, while its foot stays where it touches the
    /// ground. While the tornado forms, it starts as a low, thin, faint whirl of small puffs in its dust skirt and grows
    /// to its full height, width, density and colour; as it breaks up it stops feeding, widens and fades away. A
    /// lightning flash inside it lights every particle a cold blue.
    /// </summary>
    internal sealed class TornadoSwirl
    {
        /// <summary>Where a forming tornado starts, as shares of its full height, width and particle size.</summary>
        internal const float SeedHeight = 0.12f;
        internal const float SeedWidth = 0.35f;
        private const float SeedSize = 0.4f;

        /// <summary>How much of its full emission a tornado has as it starts to form.</summary>
        private const float SeedRate = 0.3f;

        /// <summary>How visible a forming tornado's layers are as it rises, and how fast they come in as it grows.</summary>
        internal const float SeedAlpha = 0.15f;
        internal const float AlphaGrowth = 3f;

        /// <summary>How far the top sways, as a share of the funnel's height.</summary>
        private const float Sway = 0.05f;

        /// <summary>How much wider the funnel spreads as it breaks up.</summary>
        internal const float Spread = 0.5f;

        internal static readonly Color FlashColour = new Color(0.78f, 0.88f, 1f);

        private readonly List<Part> _parts = new List<Part>();
        private TornadoShape _shape;
        private Vector3 _lean;
        private float _time;
        private float _dt;
        private float _grow;
        private float _fade = 1f;
        private float _flash;

        /// <summary>A layer's system, built once with the tornado (<see cref="TornadoParts"/>).</summary>
        public void Add(ParticleSystem system, TornadoLayer layer) =>
            _parts.Add(new Part(system, layer, new ParticleSystem.Particle[layer.MaxParticles]));

        /// <summary>
        /// A fresh tornado, <paramref name="grow"/> of the way formed: each layer emptied and, if the density leaves it
        /// anything, started - already full, as if it had been turning a while, unless it is kicked up from nothing.
        /// </summary>
        public void Begin(TornadoShape shape, float density, float grow)
        {
            _shape = shape;
            _grow = grow;
            _fade = 1f;
            foreach (Part part in _parts)
            {
                part.Rate = part.Layer.Rate * density;
                part.System.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                Feed(part); // the rate a prewarmed layer is filled at
                if (part.Rate > 0f)
                {
                    part.System.Play(false);
                }
            }
        }

        /// <summary>Each frame: <paramref name="grow"/> 0 to 1 as it forms, <paramref name="fade"/> 1 to 0 as it goes.</summary>
        public void Drive(float grow, float fade, float flash, Vector3 lean, float dt)
        {
            _time += dt;
            _dt = dt;
            _grow = grow;
            _fade = fade;
            _flash = flash;
            _lean = lean;
            foreach (Part part in _parts)
            {
                Feed(part);
                Pose(part);
            }
        }

        // The funnel thickens as it forms; the skirt kicks up its dust at full strength from the start.
        private void Feed(Part part)
        {
            float share = part.Layer.Kicked ? 1f : Mathf.Lerp(SeedRate, 1f, _grow);
            ParticleSystem.EmissionModule emission = part.System.emission;
            emission.rateOverTime = part.Rate * share;
        }

        private void Pose(Part part)
        {
            int count = part.System.GetParticles(part.Buffer);
            for (int i = 0; i < count; i++)
            {
                part.Buffer[i] = Place(part.Layer, part.Buffer[i]);
            }
            part.System.SetParticles(part.Buffer, count);
        }

        private ParticleSystem.Particle Place(TornadoLayer layer, ParticleSystem.Particle p)
        {
            float share = 1f - p.remainingLifetime / Mathf.Max(0.01f, p.startLifetime);
            float jitter = Jitter(p.randomSeed, 1u);
            float height = share * layer.Reach * _shape.Height * Mathf.Lerp(SeedHeight, 1f, _grow);
            Vector3 from = p.position - Axis(p.position.y);
            float spin = Mathf.Lerp(layer.SpinLow, layer.SpinHigh, share) * (0.8f + 0.4f * jitter);
            float angle = Mathf.Atan2(from.z, from.x) + spin * _dt;
            float radius = layer.RadiusAt(_shape, share) * Mathf.Lerp(SeedWidth, 1f, _grow)
                * (1f + Spread * (1f - _fade)) * (0.85f + 0.3f * Jitter(p.randomSeed, 2u));
            p.position = Axis(height) + new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            p.startSize = layer.SizeAt(_shape, share) * (0.75f + 0.5f * jitter) * Mathf.Lerp(SeedSize, 1f, _grow);
            p.startColor = Tint(layer, p.randomSeed, share);
            return p;
        }

        private Vector3 Axis(float height) => Bend(_shape, height, _lean, _time);

        /// <summary>The axis's offset from the foot at a height: its trail and sway, both strongest at the top. The
        /// funnel's cones (<see cref="TornadoCone"/>) bend by the same rule, at the same <see cref="Time"/>.</summary>
        internal static Vector3 Bend(TornadoShape shape, float height, Vector3 lean, float time)
        {
            float share = Mathf.Clamp01(height / Mathf.Max(1f, shape.Height));
            float bend = share * share;
            float sway = Sway * shape.Height * bend;
            return new Vector3(lean.x * bend + Mathf.Sin(time * 1.3f + share * 3f) * sway, 0f,
                lean.z * bend + Mathf.Cos(time * 1.1f + share * 2.5f) * sway);
        }

        /// <summary>Seconds it has been driven, which sets where its sway is.</summary>
        public float Time => _time;

        private Color32 Tint(TornadoLayer layer, uint seed, float share)
        {
            Color colour = Color.Lerp(layer.Dark, layer.Light, Jitter(seed, 3u));
            colour = Color.Lerp(colour, FlashColour, _flash * 0.7f);
            float appear = layer.Kicked ? 1f : Mathf.Clamp01(SeedAlpha + AlphaGrowth * _grow);
            colour.a = layer.Alpha * Mathf.Clamp01(share / 0.1f) * Mathf.Clamp01((1f - share) / 0.2f) * appear * _fade;
            return colour;
        }

        /// <summary>A steady 0..1 per particle and purpose, from the particle's own seed.</summary>
        private static float Jitter(uint seed, uint salt)
        {
            uint hash = unchecked((seed ^ (salt * 0x9E3779B9u)) * 0x85EBCA6Bu);
            hash ^= hash >> 13;
            return (hash & 0xFFFFu) / 65535f;
        }

        /// <summary>One burst of dust round its foot, as it finishes forming.</summary>
        public void Burst(int count)
        {
            foreach (Part part in _parts)
            {
                if (part.Layer.Kicked && part.Rate > 0f)
                {
                    part.System.Emit(count);
                }
            }
        }

        /// <summary>It breaks up: nothing new is fed in, and what is there fades as <see cref="Drive"/> is told.</summary>
        public void Stop()
        {
            foreach (Part part in _parts)
            {
                part.System.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>Gone: every particle cleared at once.</summary>
        public void Clear()
        {
            foreach (Part part in _parts)
            {
                part.System.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private sealed class Part
        {
            public readonly ParticleSystem System;
            public readonly TornadoLayer Layer;
            public readonly ParticleSystem.Particle[] Buffer;
            public float Rate;

            public Part(ParticleSystem system, TornadoLayer layer, ParticleSystem.Particle[] buffer)
            {
                System = system;
                Layer = layer;
                Buffer = buffer;
            }
        }
    }
}

// Portions Copyright (C) 2026 Gerald Glasgow. ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: private Bestiary namespace.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.LocalEffects
{
    /// <summary>How a one-shot burst is sized after the radius (<see cref="LocalEffect"/>).</summary>
    internal enum Sizing
    {
        Radius,
        Whole,
        Parts,
    }

    /// <summary>
    /// The sizes and densities a burst is drawn at, rounded so nearby values share one kind of kept copy
    /// (<see cref="BurstKey"/>): callers pass continuous radii (a creature's height, a radius from an RPC) and a
    /// player's density slider, and every distinct float would otherwise be a kind of its own. A radius or scale goes
    /// to the nearest step of a 4 % ladder (at most 2 % off, the same for a small burst as for a large one), a density
    /// to the nearest 0.05 (never below 0.05, so a thin burst never becomes none).
    /// </summary>
    internal static class BurstSizes
    {
        private const float SizeStep = 1.04f;
        private const float DensityStep = 0.05f;
        private static readonly float LogStep = Mathf.Log(SizeStep);

        public static float Size(float value)
        {
            if (value <= 0.01f)
            {
                return value;
            }
            return Mathf.Exp(Mathf.Round(Mathf.Log(value) / LogStep) * LogStep);
        }

        public static float Density(float value) => Mathf.Max(DensityStep, Mathf.Round(value / DensityStep) * DensityStep);
    }

    /// <summary>
    /// The kind of a kept burst (<see cref="BurstPool"/>): the same prefab sized and thinned the same way (the numbers
    /// already rounded by <see cref="BurstSizes"/>). Compared as numbers, so a lookup allocates nothing.
    /// </summary>
    internal readonly struct BurstKey : IEquatable<BurstKey>
    {
        private readonly int prefab;
        private readonly int sizing;
        private readonly float radius;
        private readonly float scale;
        private readonly float density;

        public BurstKey(GameObject prefab, Sizing sizing, float radius, float scale, float density)
        {
            this.prefab = prefab.GetInstanceID();
            this.sizing = (int)sizing;
            this.radius = radius;
            this.scale = scale;
            this.density = density;
        }

        public bool Equals(BurstKey other) => prefab == other.prefab && sizing == other.sizing && radius == other.radius &&
            scale == other.scale && density == other.density;

        public override bool Equals(object? obj) => obj is BurstKey other && Equals(other);

        public override int GetHashCode() =>
            unchecked((((prefab * 31 + sizing) * 31 + radius.GetHashCode()) * 31 + scale.GetHashCode()) * 31 + density.GetHashCode());
    }

    /// <summary>On a kept burst copy: hands it back to <see cref="BurstPool"/> when its time is up.</summary>
    internal sealed class PooledBurst : MonoBehaviour
    {
        private ParticleSystem[] systems = Array.Empty<ParticleSystem>();
        private float life;

        public BurstKey Key { get; private set; }

        public void Begin(BurstKey key, float seconds, ParticleSystem[] parts)
        {
            Key = key;
            life = seconds;
            systems = parts;
            Live();
        }

        /// <summary>Shown (again): back to the pool after the copy's time.</summary>
        public void Live() => Invoke(nameof(Spend), life);

        private void Spend() => BurstPool.Spend(this, systems);
    }
}

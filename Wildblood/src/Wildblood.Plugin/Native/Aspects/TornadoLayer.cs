// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One layer of the particles round a Nightfall tornado's funnel, as numbers: the particles of each layer rise and
    /// spin round the funnel's axis on a path of their own (<see cref="TornadoSwirl"/>). The funnel itself is a mesh
    /// (<see cref="TornadoCone"/>), drawn whatever the effect density because it is the hazard; the cloud whirling on its
    /// surface, the debris cloud churning round its lower part, the grit flung about in it and the dust skirt it kicks
    /// up at its foot are thinned by the density like any other effect. All are the colour of earth and storm, unlit so they still read in the dead of a
    /// stormy night.
    /// </summary>
    internal sealed class TornadoLayer
    {
        /// <summary>Cloud whirling on the funnel's surface, over its cones: the turning a player sees.</summary>
        public static readonly TornadoLayer Funnel = new TornadoLayer
        {
            Name = "funnel", Rate = 90f, Lifetime = 2.2f, SizeFactor = 1.1f, MinSize = 0.5f, Alpha = 0.7f, SpinLow = 9f,
            SpinHigh = 3.75f, Dark = new Color(0.22f, 0.23f, 0.26f), Light = new Color(0.40f, 0.41f, 0.45f),
        };

        /// <summary>The debris cloud: dust and torn earth churning round the funnel's lower third, wider than it.</summary>
        public static readonly TornadoLayer Sheath = new TornadoLayer
        {
            Name = "debris", Rate = 34f, Lifetime = 2.4f, BaseScale = 2.6f, TopScale = 0.6f, Reach = 0.35f,
            SizeFactor = 0.9f, MinSize = 1f, Alpha = 0.55f, SpinLow = 6f, SpinHigh = 4f,
            Dark = new Color(0.20f, 0.19f, 0.18f), Light = new Color(0.36f, 0.34f, 0.31f),
        };

        /// <summary>Grit, twigs and stones whipped round its lower half.</summary>
        public static readonly TornadoLayer Grit = new TornadoLayer
        {
            Name = "grit", Rate = 40f, Lifetime = 1.6f, BaseScale = 2.5f, TopScale = 1f, Reach = 0.5f, MinSize = 0.07f,
            MaxSize = 0.22f, Alpha = 1f, SpinLow = 10.5f, SpinHigh = 6f, Dark = new Color(0.10f, 0.08f, 0.06f),
            Light = new Color(0.22f, 0.18f, 0.14f), Specks = true,
        };

        /// <summary>The dust skirt at its foot: low, wide and spreading, the first thing seen as it whirls up.</summary>
        public static readonly TornadoLayer Skirt = new TornadoLayer
        {
            Name = "skirt", Rate = 30f, Lifetime = 1.4f, BaseScale = 1.6f, TopScale = 2.2f, Reach = 0.12f,
            SizeFactor = 0.8f, MinSize = 0.8f, Alpha = 0.7f, SpinLow = 4.5f, SpinHigh = 2.25f,
            Dark = new Color(0.26f, 0.23f, 0.19f), Light = new Color(0.40f, 0.36f, 0.30f), Kicked = true,
        };

        public string Name = "";

        /// <summary>
        /// Kicked up from nothing at full strength the moment the tornado rises. Every other layer is there at once, as
        /// if it had been turning for a while, squeezed into the small whirl the tornado starts as, and fades in and
        /// thickens as it grows.
        /// </summary>
        public bool Kicked;

        /// <summary>Particles a second at full strength.</summary>
        public float Rate;

        /// <summary>Seconds a particle takes to climb from the foot to the top of its layer.</summary>
        public float Lifetime;

        /// <summary>The layer's radius at its foot, times the funnel's base radius.</summary>
        public float BaseScale = 1f;

        /// <summary>The layer's radius at its top, times the funnel's top radius (whatever height the layer reaches).</summary>
        public float TopScale = 1f;

        /// <summary>How high the layer climbs, as a share of the funnel's height.</summary>
        public float Reach = 1f;

        /// <summary>A particle's size against the layer's radius where it is; 0 for a fixed size (grit).</summary>
        public float SizeFactor;

        public float MinSize;
        public float MaxSize;
        public float Alpha;

        /// <summary>Radians a second round the axis at the layer's foot and at its top.</summary>
        public float SpinLow;
        public float SpinHigh;

        public Color Dark;
        public Color Light;

        /// <summary>Drawn as small hard specks rather than soft smoke.</summary>
        public bool Specks;

        public int MaxParticles => Mathf.CeilToInt(Rate * Lifetime * 1.5f) + 10;

        /// <summary>The layer's radius at <paramref name="share"/> of its climb.</summary>
        public float RadiusAt(TornadoShape shape, float share) =>
            TornadoShape.Between(shape.BaseRadius * BaseScale, shape.TopRadius * TopScale, share);

        /// <summary>A particle's size at <paramref name="share"/> of its climb, before the per-particle spread.</summary>
        public float SizeAt(TornadoShape shape, float share)
        {
            if (SizeFactor <= 0f)
            {
                return Mathf.Lerp(MinSize, MaxSize, share);
            }
            return Mathf.Max(MinSize, RadiusAt(shape, share) * SizeFactor);
        }
    }
}

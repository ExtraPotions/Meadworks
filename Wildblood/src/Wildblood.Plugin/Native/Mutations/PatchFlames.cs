// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn, GPL-3.0-or-later
// Flame visuals adapted from ECR f331465b3ed2551c89decc42ba81565a7b689543
// Changed by expDARE, 2026: owned lifecycle, portable emission policy and live ember fallback.
using ExtraPotions.Wildblood.Visuals;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The flames licking up from Flamebound's burning patches on this machine: one local copy of the game's own Burning
    /// look (the flames it draws on a burning body, <see cref="CosmeticClone"/>, never networked), its flame and cinder
    /// systems fed by hand in world space, each at half the game's own rate for a burning body and rising as the game
    /// lifts them, from a random point inside a live patch (fed sparser, the game's crisp flame frames read as scattered
    /// orange shards, not a fire). One copy draws every creature's fire, so a long trail costs particles, never objects,
    /// and a patch stops burning the moment it is gone. Thinned by the player's effect density, and none at all at 0, as
    /// every effect of the mod; the ember decal then shows the patch instead. Never on a dedicated server.
    /// </summary>
    internal sealed class PatchFlames
    {
        private const string Source = "vfx_Burning";
        private static readonly string[] Keywords = { "burning", "flame", "fire" };

        /// <summary>How far from a patch's middle a flame may rise, as a share of its reach.</summary>
        private const float Spread = 0.75f;

        /// <summary>The flames' size as a share of the game's on a burning body (their rise shrinks with them).</summary>
        private const float FlameSize = 0.6f;

        /// <summary>A part of the game's effect whose particles live longer than this is not a flame.</summary>
        private const float MaxLife = 10f;

        private readonly ParticleSystem[] _systems;
        private readonly float[] _rates;
        private readonly float[] _owed;

        private PatchFlames(List<ParticleSystem> systems, List<float> rates)
        {
            _systems = systems.ToArray();
            _rates = rates.ToArray();
            _owed = new float[_systems.Length];
        }

        /// <summary>The flames, copied under <paramref name="parent"/>; null when effects are off or the game has none.</summary>
        public static PatchFlames? Build(Transform parent)
        {
            GameObject? prefab = EffectResolver.Resolve(Source, Keywords, "Flamebound flames");
            GameObject? copy = CosmeticClone.Spawn(prefab, parent, parent.position, endless: true);
            if (copy == null)
            {
                return null;
            }
            Quiet(copy);
            copy.transform.localScale = Vector3.one * FlameSize; // every part scales with the copy (Hierarchy)
            PatchFlames? flames = Flames(copy);
            if (flames == null) Object.Destroy(copy);
            return flames;
        }

        /// <summary>True while this player draws effects at all; at 0 there are no flames, so the decal warns instead.</summary>
        public static bool Shown => Density() > 0f;

        // Only the flames and cinders the game itself keeps burning are fed. Its flare (one 4 m glow that never dies)
        // and its spray of drops (which it never draws) go: fed by hand, they piled up into a glowing haze that fog
        // turned into a bright white wall around the creature.
        private static PatchFlames? Flames(GameObject copy)
        {
            List<ParticleSystem> flames = new List<ParticleSystem>();
            List<float> rates = new List<float>();
            foreach (ParticleSystem system in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (Burns(system))
                {
                    rates.Add(FlameVisualPolicy.PerPatchRate(system.emission.rateOverTimeMultiplier, Density()));
                    Settle(system);
                    flames.Add(system);
                }
                else
                {
                    Silence(system);
                }
            }
            return flames.Count > 0 ? new PatchFlames(flames, rates) : null;
        }

        // Drawn in the game's own effect (an active part), more than one at a time, and each gone within seconds.
        private static bool Burns(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            return system.gameObject.activeInHierarchy && main.maxParticles > 1 && Longest(main.startLifetime) < MaxLife;
        }

        private static float Longest(ParticleSystem.MinMaxCurve life) => life.mode switch
        {
            ParticleSystemCurveMode.Constant => life.constant,
            ParticleSystemCurveMode.TwoConstants => life.constantMax,
            _ => life.curveMultiplier,
        };

        private static void Silence(ParticleSystem system)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        // Its light and crackle stay where the copy sits, far from any fire, so both go: the flames alone are drawn.
        private static void Quiet(GameObject copy)
        {
            foreach (Behaviour part in copy.GetComponentsInChildren<Behaviour>(true))
            {
                if (part is Light || part is AudioSource || part is ZSFX || part is LightLod || part is LightFlicker)
                {
                    part.enabled = false;
                }
            }
        }

        // Emptied, emitting nothing of its own, simulated in the world even out of sight, then fed by Tick alone.
        private static void Settle(ParticleSystem system)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = system.main;
            main.stopAction = ParticleSystemStopAction.None; // emptied above, it must not remove or hide itself
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.maxParticles = Mathf.Max(main.maxParticles, 2000);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false; // the game's shape follows a body; here each flame is placed by hand
            system.Play(false);
        }

        /// <summary>Lights the flames owed over the last <paramref name="elapsed"/> seconds, spread over the patches.</summary>
        public void Tick(List<GroundPatch> patches, float elapsed)
        {
            for (int i = 0; i < _systems.Length; i++)
            {
                int count = FlameVisualPolicy.Take(ref _owed[i], _rates[i], patches.Count, elapsed, Density());
                for (int emitted = 0; emitted < count; emitted++)
                {
                    Emit(_systems[i], Within(patches[Random.Range(0, patches.Count)]));
                }
            }
        }

        // Only where: speed, size, life and the rise come from the game's own part.
        private static void Emit(ParticleSystem system, Vector3 at)
        {
            if (system != null)
            {
                system.Emit(new ParticleSystem.EmitParams { position = at }, 1);
            }
        }

        private static Vector3 Within(GroundPatch patch)
        {
            Vector2 offset = Random.insideUnitCircle * Mathf.Sqrt(patch.RadiusSq) * Spread;
            return patch.Point + new Vector3(offset.x, 0.05f, offset.y);
        }

        private static float Density() => Mathf.Clamp01(Configuration.EffectDensity.Value);
    }
}

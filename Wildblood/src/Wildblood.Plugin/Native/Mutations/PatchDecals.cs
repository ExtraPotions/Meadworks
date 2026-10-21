// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The drawing of every patch of one trail kind on this machine: one local copy of a game ground-decal system
    /// (<see cref="DecalSource"/>), never networked, into which each patch is emitted exactly the way the game's own
    /// <c>ParticleDecal</c> lays a splat - at the ground point, turned to lie on the surface - so it bends over the
    /// slope and the stones it fell on like any blood or slime splat does. It wears the kind's own solid patch shape
    /// (<see cref="PatchTexture"/>) in place of the game's droplets. Each patch is one particle: the kind's colour, the
    /// patch's width, and a life that ends when the patch does; it swells in over its first moment and fades out over
    /// its last tenth. Nothing moves, spawns or collides. One system draws every creature's patches, so a hundred
    /// patches cost one particle system, and a patch outlives the creature that laid it. Drawn whatever the effect
    /// density: the patch is the hazard's only warning, as Stormbound's circle is.
    /// </summary>
    internal sealed class PatchDecals
    {
        /// <summary>The most patches one system draws at once; far more than every trail in sight lays.</summary>
        private const int MaxParticles = 1000;

        /// <summary>The share of a patch's life it takes to appear, and to fade out.</summary>
        private const float FadeIn = 0.04f;
        private const float FadeOut = 0.1f;

        /// <summary>
        /// How much wider a patch is drawn than it reaches, so its pool's soft edge falls on the line where it stops
        /// working.
        /// </summary>
        private const float Overdraw = 1.1f;

        private const string MainTexture = "_MainTex";
        private const string ColourProperty = "_Color";

        private readonly ParticleSystem? _system;
        private readonly bool _size3D;

        private PatchDecals(ParticleSystem? system)
        {
            _system = system;
            _size3D = system != null && system.main.startSize3D;
        }

        /// <summary>
        /// A drawer for <paramref name="kind"/>'s patches through the decal <paramref name="source"/>, its copy under
        /// <paramref name="parent"/>; a null source draws nothing.
        /// </summary>
        public static PatchDecals Build(ParticleSystem? source, Transform parent, TrailKind kind)
        {
            if (source == null)
            {
                return new PatchDecals(null);
            }
            ParticleSystem system = Copy(source.gameObject, parent).GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Settle(system);
            Still(system);
            Quiet(system);
            Fade(system);
            Dress(system, kind);
            system.Play();
            return new PatchDecals(system);
        }

        /// <summary>
        /// One patch: <paramref name="diameter"/> wide at <paramref name="point"/>, gone in <paramref name="life"/>
        /// seconds.
        /// </summary>
        public void Draw(Vector3 point, Vector3 normal, float diameter, float life, Color colour, long id)
        {
            if (_system == null)
            {
                return;
            }
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = point,
                rotation3D = Facing(normal, id),
                velocity = -normal * 0.001f, // the game's own decals carry the surface they lie on this way
                startLifetime = life,
                startColor = colour,
            };
            float size = diameter * Overdraw;
            if (_size3D)
            {
                emit.startSize3D = new Vector3(size, size, size);
            }
            else
            {
                emit.startSize = size;
            }
            _system.Emit(emit, 1);
        }

        // Keep the existing warning particles alive while flames draw, so density zero reveals every live patch.
        internal void Show(bool visible)
        {
            ParticleSystemRenderer renderer = _system != null ? _system.GetComponent<ParticleSystemRenderer>() : null!;
            if (renderer != null) renderer.enabled = visible;
        }

        // Turned the way the game turns a splat to lie on a surface, and spun about it by the drop's id, so every
        // machine draws the same patch the same way round.
        private static Vector3 Facing(Vector3 normal, long id)
        {
            Vector3 euler = Quaternion.LookRotation(normal).eulerAngles;
            euler.x = 180f - euler.x;
            euler.y = -euler.y;
            euler.z = id * 137L % 360L;
            return euler;
        }

        // The decal object alone, copied with network views held asleep (it should have none), at the parent's origin.
        private static GameObject Copy(GameObject source, Transform parent)
        {
            bool was = ZNetView.m_forceDisableInit;
            ZNetView.m_forceDisableInit = true;
            try
            {
                GameObject copy = Object.Instantiate(source, parent);
                copy.name = "ecr_patch_decals";
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = Vector3.one;
                Strip(copy);
                copy.SetActive(true);
                return copy;
            }
            finally
            {
                ZNetView.m_forceDisableInit = was;
            }
        }

        // Only the decal system itself stays: its children, scripts, lights, sounds and colliders go.
        private static void Strip(GameObject copy)
        {
            for (int i = copy.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(copy.transform.GetChild(i).gameObject);
            }
            foreach (MonoBehaviour script in copy.GetComponents<MonoBehaviour>())
            {
                Object.Destroy(script);
            }
            foreach (Behaviour part in copy.GetComponents<Behaviour>())
            {
                if (part is Light || part is AudioSource)
                {
                    part.enabled = false;
                }
            }
            foreach (Collider collider in copy.GetComponents<Collider>())
            {
                collider.enabled = false;
            }
        }

        // Fed by hand, in world space, simulated even out of sight so it keeps the same clock as the patches it draws.
        private static void Settle(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None; // never removes or hides itself
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = MaxParticles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.gravityModifier = 0f;
            main.simulationSpeed = 1f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.maxParticleSize = 10f; // a wide patch seen from close is never shrunk to fit the screen
            }
        }

        // Nothing may move or spin: a patch lies where it fell, whatever wind or force field it lies in.
        private static void Still(ParticleSystem system)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = false;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = false;
            ParticleSystem.LimitVelocityOverLifetimeModule limit = system.limitVelocityOverLifetime;
            limit.enabled = false;
            ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
            force.enabled = false;
            ParticleSystem.ExternalForcesModule external = system.externalForces;
            external.enabled = false;
            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = false;
            ParticleSystem.RotationBySpeedModule spinBySpeed = system.rotationBySpeed;
            spinBySpeed.enabled = false;
        }

        // Nor collide, nor spawn anything of its own: no shape, collisions, sub-emitters, trails or lights.
        private static void Quiet(ParticleSystem system)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = false;
            ParticleSystem.SubEmittersModule sub = system.subEmitters;
            sub.enabled = false;
            ParticleSystem.TrailModule trails = system.trails;
            trails.enabled = false;
            ParticleSystem.LightsModule lights = system.lights;
            lights.enabled = false;
        }

        // The game's splat is a scatter of droplets; a patch is solid ground, so the copy wears the kind's own patch
        // texture (PatchTexture) over the game's decal shader and settings - whenever that shader takes a main
        // texture - with the material's own colour (a tar splat's black) set to white, so the kind's colour alone
        // tints it.
        private static void Dress(ParticleSystem system, TrailKind kind)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            Material? game = renderer != null ? renderer.sharedMaterial : null;
            if (game == null || !game.HasProperty(MainTexture))
            {
                return; // drawn as the game's splat instead, tinted all the same
            }
            Material own = new Material(game) { name = $"ecr_{kind.Name}_patch", mainTexture = PatchTexture.For(kind) };
            own.mainTextureScale = Vector2.one;
            own.mainTextureOffset = Vector2.zero;
            if (own.HasProperty(ColourProperty))
            {
                own.color = Color.white;
            }
            renderer!.sharedMaterial = own;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = false; // one whole patch per particle, never a tile of the game's sheet
        }

        // Swells to full width in its first moment, holds, and thins away over the last tenth of its life.
        private static void Fade(ParticleSystem system)
        {
            ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(FadeGradient());
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = false;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.7f), new Keyframe(FadeIn, 1f), new Keyframe(1f, 1f)));
            ParticleSystem.ColorBySpeedModule bySpeed = system.colorBySpeed;
            bySpeed.enabled = false;
            ParticleSystem.SizeBySpeedModule sizeBySpeed = system.sizeBySpeed;
            sizeBySpeed.enabled = false;
        }

        private static Gradient FadeGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, FadeIn),
                    new GradientAlphaKey(1f, 1f - FadeOut), new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }
    }
}

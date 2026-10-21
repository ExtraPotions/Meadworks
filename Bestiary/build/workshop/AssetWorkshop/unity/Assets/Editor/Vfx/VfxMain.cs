using System;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>The main module, the shape and the emission of a particle system, from the spec.</summary>
    public static class VfxMain
    {
        public static void Apply(ParticleSystem ps, SystemSpec s, Func<string, Mesh> meshes)
        {
            Main(ps, s);
            Start(ps, s);
            Shape(ps, s.shape, meshes);
            Emission(ps, s.emission);
        }

        private static void Main(ParticleSystem ps, SystemSpec s)
        {
            var main = ps.main;
            main.duration = Mathf.Max(0.05f, s.duration);
            main.loop = s.loop;
            main.prewarm = s.prewarm;
            main.startDelay = VfxCurves.Curve(s.delay);
            main.playOnAwake = s.play_on_awake;
            main.simulationSpace = s.space == "world" ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = s.scaling == "hierarchy" ? ParticleSystemScalingMode.Hierarchy
                : s.scaling == "shape" ? ParticleSystemScalingMode.Shape : ParticleSystemScalingMode.Local;
            main.stopAction = s.stop_action == "destroy" ? ParticleSystemStopAction.Destroy
                : s.stop_action == "disable" ? ParticleSystemStopAction.Disable : ParticleSystemStopAction.None;
            main.simulationSpeed = s.sim_speed;
            main.maxParticles = s.max_particles;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            ps.useAutoRandomSeed = s.seed == 0;
            if (s.seed != 0)
                ps.randomSeed = (uint)s.seed;
        }

        private static void Start(ParticleSystem ps, SystemSpec s)
        {
            var main = ps.main;
            main.startLifetime = VfxCurves.Curve(s.lifetime);
            main.startSpeed = VfxCurves.Curve(s.speed);
            main.startSize3D = s.size3d;
            main.startSize = VfxCurves.Curve(s.size);
            if (s.size3d)
            {
                main.startSizeX = VfxCurves.Curve(s.size);
                main.startSizeY = VfxCurves.Curve(s.size_y);
                main.startSizeZ = VfxCurves.Curve(s.size);
            }
            main.startRotation = VfxCurves.Curve(Scaled(s.rotation, Mathf.Deg2Rad));
            main.flipRotation = s.flip_rotation;
            main.startColor = VfxCurves.Gradient(s.colour);
            main.gravityModifier = VfxCurves.Curve(s.gravity);
        }

        /// <summary>A copy of a curve with every value multiplied (degrees to radians).</summary>
        public static Curve Scaled(Curve c, float k)
        {
            if (c == null)
                return null;
            var v = Array.ConvertAll(c.v, x => x * k);
            var v2 = Array.ConvertAll(c.v2, x => x * k);
            return new Curve { mode = c.mode, c = c.c * k, min = c.min * k, max = c.max * k, t = c.t, v = v, t2 = c.t2, v2 = v2 };
        }

        private static void Shape(ParticleSystem ps, ShapeSpec s, Func<string, Mesh> meshes)
        {
            var shape = ps.shape;
            shape.enabled = s != null && s.enabled;
            if (!shape.enabled)
                return;
            shape.shapeType = ShapeType(s.type);
            shape.angle = s.angle;
            shape.radius = s.radius;
            shape.radiusThickness = s.thickness;
            shape.arc = s.arc;
            shape.length = s.length;
            shape.donutRadius = s.donut;
            shape.position = VfxCurves.Vector(s.position);
            shape.rotation = VfxCurves.Vector(s.rotation);
            shape.scale = VfxCurves.Vector(s.scale);
            shape.alignToDirection = s.align_to_direction;
            shape.randomDirectionAmount = s.random_direction;
            shape.sphericalDirectionAmount = s.spherize;
            shape.randomPositionAmount = s.random_position;
            if (!string.IsNullOrEmpty(s.mesh))
                shape.mesh = meshes(s.mesh);
        }

        public static ParticleSystemShapeType ShapeType(string type)
        {
            switch (type)
            {
                case "sphere": return ParticleSystemShapeType.Sphere;
                case "hemisphere": return ParticleSystemShapeType.Hemisphere;
                case "cone_volume": return ParticleSystemShapeType.ConeVolume;
                case "box": return ParticleSystemShapeType.Box;
                case "box_shell": return ParticleSystemShapeType.BoxShell;
                case "box_edge": return ParticleSystemShapeType.BoxEdge;
                case "circle": return ParticleSystemShapeType.Circle;
                case "edge": return ParticleSystemShapeType.SingleSidedEdge;
                case "donut": return ParticleSystemShapeType.Donut;
                case "rectangle": return ParticleSystemShapeType.Rectangle;
                case "mesh": return ParticleSystemShapeType.Mesh;
                default: return ParticleSystemShapeType.Cone;
            }
        }

        private static void Emission(ParticleSystem ps, EmissionSpec e)
        {
            var emission = ps.emission;
            emission.enabled = e != null && e.enabled;
            if (!emission.enabled)
                return;
            emission.rateOverTime = VfxCurves.Curve(e.rate);
            emission.rateOverDistance = VfxCurves.Curve(e.rate_distance);
            var bursts = new ParticleSystem.Burst[e.bursts.Length];
            for (int i = 0; i < bursts.Length; i++)
            {
                BurstSpec b = e.bursts[i];
                bursts[i] = new ParticleSystem.Burst(b.time, VfxCurves.Curve(b.count), b.cycles, b.interval) { probability = b.probability };
            }
            emission.SetBursts(bursts);
        }
    }
}

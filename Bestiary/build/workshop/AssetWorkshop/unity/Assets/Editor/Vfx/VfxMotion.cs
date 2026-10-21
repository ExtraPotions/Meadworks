using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>The modules that move particles after they are born: velocity, limit (drag), force and noise.</summary>
    public static class VfxMotion
    {
        public static void Apply(ParticleSystem ps, SystemSpec s)
        {
            Velocity(ps, s.velocity);
            Limit(ps, s.limit);
            Force(ps, s.force);
            Noise(ps, s.noise);
        }

        private static void Velocity(ParticleSystem ps, VelocitySpec v)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = v != null && v.enabled;
            if (!velocity.enabled)
                return;
            velocity.space = v.world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            velocity.x = VfxCurves.Curve(v.x);
            velocity.y = VfxCurves.Curve(v.y);
            velocity.z = VfxCurves.Curve(v.z);
            velocity.orbitalX = VfxCurves.Curve(v.orbital_x);
            velocity.orbitalY = VfxCurves.Curve(v.orbital_y);
            velocity.orbitalZ = VfxCurves.Curve(v.orbital_z);
            velocity.radial = VfxCurves.Curve(v.radial);
            velocity.speedModifier = VfxCurves.Curve(v.speed_modifier);
        }

        private static void Limit(ParticleSystem ps, LimitSpec l)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = l != null && l.enabled;
            if (!limit.enabled)
                return;
            limit.limit = VfxCurves.Curve(l.speed);
            limit.dampen = l.dampen;
            limit.drag = VfxCurves.Curve(l.drag);
            limit.multiplyDragByParticleVelocity = true;
        }

        private static void Force(ParticleSystem ps, ForceSpec f)
        {
            var force = ps.forceOverLifetime;
            force.enabled = f != null && f.enabled;
            if (!force.enabled)
                return;
            force.space = f.world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            force.x = VfxCurves.Curve(f.x);
            force.y = VfxCurves.Curve(f.y);
            force.z = VfxCurves.Curve(f.z);
            force.randomized = f.random_per_frame;
        }

        private static void Noise(ParticleSystem ps, NoiseSpec n)
        {
            var noise = ps.noise;
            noise.enabled = n != null && n.enabled;
            if (!noise.enabled)
                return;
            noise.strength = VfxCurves.Curve(n.strength);
            noise.frequency = n.frequency;
            noise.scrollSpeed = VfxCurves.Curve(n.scroll);
            noise.damping = n.damping;
            noise.octaveCount = Mathf.Clamp(n.octaves, 1, 4);
            noise.quality = n.quality <= 0 ? ParticleSystemNoiseQuality.Low
                : n.quality == 1 ? ParticleSystemNoiseQuality.Medium : ParticleSystemNoiseQuality.High;
            noise.positionAmount = VfxCurves.Curve(n.position);
            noise.rotationAmount = VfxCurves.Curve(n.rotation);
            noise.sizeAmount = VfxCurves.Curve(n.size);
        }
    }
}

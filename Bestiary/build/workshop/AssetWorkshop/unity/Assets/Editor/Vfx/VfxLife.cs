using System;
using UnityEngine;

namespace Workshop.Vfx
{
    /// <summary>
    /// The modules that change a particle over its life: colour, size and rotation over lifetime, the texture sheet,
    /// custom data (the gradient-mapped shader's two colours), trails and collision. Sub-emitters are linked after
    /// every system exists (<see cref="Link"/>).
    /// </summary>
    public static class VfxLife
    {
        public static void Apply(ParticleSystem ps, SystemSpec s)
        {
            OverLife(ps, s);
            Sheet(ps, s.sheet);
            Custom(ps, s.custom1, s.custom2);
            Trails(ps, s.trail);
            Collision(ps, s.collision);
        }

        private static void OverLife(ParticleSystem ps, SystemSpec s)
        {
            var colour = ps.colorOverLifetime;
            colour.enabled = s.colour_life != null && s.colour_life.enabled;
            if (colour.enabled)
                colour.color = VfxCurves.Gradient(s.colour_life.gradient);
            var size = ps.sizeOverLifetime;
            size.enabled = s.size_life != null && s.size_life.enabled;
            if (size.enabled)
                size.size = VfxCurves.Curve(s.size_life.curve);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = s.rotation_life != null && s.rotation_life.enabled;
            if (rotation.enabled)
                rotation.z = VfxCurves.Curve(VfxMain.Scaled(s.rotation_life.curve, Mathf.Deg2Rad));
        }

        private static void Sheet(ParticleSystem ps, SheetSpec s)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = s != null && s.enabled;
            if (!sheet.enabled)
                return;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = Mathf.Max(1, s.tiles_x);
            sheet.numTilesY = Mathf.Max(1, s.tiles_y);
            sheet.animation = s.single_row ? ParticleSystemAnimationType.SingleRow : ParticleSystemAnimationType.WholeSheet;
            sheet.rowMode = s.random_row ? ParticleSystemAnimationRowMode.Random : ParticleSystemAnimationRowMode.Custom;
            sheet.timeMode = s.time == "fps" ? ParticleSystemAnimationTimeMode.FPS
                : s.time == "speed" ? ParticleSystemAnimationTimeMode.Speed : ParticleSystemAnimationTimeMode.Lifetime;
            sheet.fps = s.fps;
            sheet.frameOverTime = VfxCurves.Curve(s.frame);
            sheet.startFrame = VfxCurves.Curve(s.start_frame);
            sheet.cycleCount = Mathf.Max(1, Mathf.RoundToInt(s.cycles));
        }

        private static void Custom(ParticleSystem ps, CustomSpec first, CustomSpec second)
        {
            var custom = ps.customData;
            bool any = Mode(first) != ParticleSystemCustomDataMode.Disabled || Mode(second) != ParticleSystemCustomDataMode.Disabled;
            custom.enabled = any;
            if (!any)
                return;
            Stream(custom, ParticleSystemCustomData.Custom1, first);
            Stream(custom, ParticleSystemCustomData.Custom2, second);
        }

        private static ParticleSystemCustomDataMode Mode(CustomSpec c) =>
            c == null || c.mode == "none" ? ParticleSystemCustomDataMode.Disabled
            : c.mode == "colour" ? ParticleSystemCustomDataMode.Color : ParticleSystemCustomDataMode.Vector;

        private static void Stream(ParticleSystem.CustomDataModule custom, ParticleSystemCustomData stream, CustomSpec c)
        {
            custom.SetMode(stream, Mode(c));
            if (Mode(c) == ParticleSystemCustomDataMode.Color)
                custom.SetColor(stream, VfxCurves.Gradient(c.colour));
            if (Mode(c) != ParticleSystemCustomDataMode.Vector)
                return;
            custom.SetVectorComponentCount(stream, c.vector.Length);
            for (int i = 0; i < c.vector.Length; i++)
                custom.SetVector(stream, i, VfxCurves.Curve(c.vector[i]));
        }

        private static void Trails(ParticleSystem ps, TrailSpec t)
        {
            var trails = ps.trails;
            trails.enabled = t != null && t.enabled;
            if (!trails.enabled)
                return;
            trails.mode = t.mode == "ribbon" ? ParticleSystemTrailMode.Ribbon : ParticleSystemTrailMode.PerParticle;
            trails.ratio = t.ratio;
            trails.lifetime = VfxCurves.Curve(t.lifetime);
            trails.minVertexDistance = t.min_vertex_distance;
            trails.textureMode = (ParticleSystemTrailTextureMode)t.texture_mode;
            trails.worldSpace = t.world;
            trails.widthOverTrail = VfxCurves.Curve(t.width);
            trails.colorOverLifetime = VfxCurves.Gradient(t.colour_life);
            trails.colorOverTrail = VfxCurves.Gradient(t.colour_trail);
            trails.inheritParticleColor = t.inherit_colour;
            trails.dieWithParticles = t.die_with_particles;
            trails.sizeAffectsWidth = t.size_affects_width;
        }

        private static void Collision(ParticleSystem ps, CollisionSpec c)
        {
            var collision = ps.collision;
            collision.enabled = c != null && c.enabled;
            if (!collision.enabled)
                return;
            collision.type = c.type == "planes" ? ParticleSystemCollisionType.Planes : ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = VfxCurves.Curve(c.dampen);
            collision.bounce = VfxCurves.Curve(c.bounce);
            collision.lifetimeLoss = VfxCurves.Curve(c.lifetime_loss);
            collision.radiusScale = c.radius_scale;
            collision.quality = c.quality <= 0 ? ParticleSystemCollisionQuality.High
                : c.quality == 1 ? ParticleSystemCollisionQuality.Medium : ParticleSystemCollisionQuality.Low;
            collision.sendCollisionMessages = c.send_messages;
        }

        /// <summary>Sub-emitters name another system of the effect; `find` looks it up once every system exists.</summary>
        public static void Link(ParticleSystem ps, SystemSpec s, Func<string, ParticleSystem> find)
        {
            var sub = ps.subEmitters;
            sub.enabled = s.sub_emitters.Length > 0;
            foreach (SubSpec e in s.sub_emitters)
            {
                ParticleSystem emitter = find(e.emitter) ?? throw new InvalidOperationException(s.name + ": no sub-emitter " + e.emitter);
                sub.AddSubEmitter(emitter, SubType(e.type), (ParticleSystemSubEmitterProperties)e.inherit, e.probability);
            }
        }

        private static ParticleSystemSubEmitterType SubType(string type) =>
            type == "birth" ? ParticleSystemSubEmitterType.Birth
            : type == "collision" ? ParticleSystemSubEmitterType.Collision
            : type == "trigger" ? ParticleSystemSubEmitterType.Trigger
            : type == "manual" ? ParticleSystemSubEmitterType.Manual : ParticleSystemSubEmitterType.Death;
    }
}

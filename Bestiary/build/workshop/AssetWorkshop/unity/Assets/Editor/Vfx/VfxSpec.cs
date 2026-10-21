using System;
using System.IO;
using UnityEngine;

namespace Workshop.Vfx
{
    // The effect spec as AssetWorkshop/vfx/spec.py writes it (effect.json): every field present, curves and colours in
    // flat forms JsonUtility can read. Field names are the codex's words, so they are snake_case like the JSON.
#pragma warning disable IDE1006

    /// <summary>A MinMaxCurve: mode const (c), range (min..max), curve (t, v) or curves (t, v and t2, v2).</summary>
    [Serializable] public class Curve
    {
        public string mode = "const";
        public float c, min, max, scale = 1f;
        public float[] t = new float[0], v = new float[0], t2 = new float[0], v2 = new float[0];
    }

    /// <summary>A MinMaxGradient: colour (a), colours (a, b), gradient/random (ck, ak) or gradients (ck, ak, ck2, ak2).
    /// Colour keys are flat t, r, g, b quadruples, alpha keys t, a pairs.</summary>
    [Serializable] public class Grad
    {
        public string mode = "colour";
        public float[] a = { 1, 1, 1, 1 }, b = { 1, 1, 1, 1 };
        public float[] ck = new float[0], ak = new float[0], ck2 = new float[0], ak2 = new float[0];
    }

    [Serializable] public class ShapeSpec
    {
        public bool enabled = true, align_to_direction;
        public string type = "cone", mesh = "";
        public float angle = 25, radius = 1, thickness = 1, arc = 360, length = 5, donut = 0.2f;
        public float random_direction, spherize, random_position;
        public float[] position = { 0, 0, 0 }, rotation = { 0, 0, 0 }, scale = { 1, 1, 1 };
    }

    [Serializable] public class BurstSpec
    {
        public float time, interval = 0.01f, probability = 1;
        public Curve count = new Curve();
        public int cycles = 1;
    }

    [Serializable] public class EmissionSpec
    {
        public bool enabled = true;
        public Curve rate = new Curve(), rate_distance = new Curve();
        public BurstSpec[] bursts = new BurstSpec[0];
    }

    [Serializable] public class VelocitySpec
    {
        public bool enabled, world;
        public Curve x, y, z, radial, speed_modifier, orbital_x, orbital_y, orbital_z;
    }

    [Serializable] public class LimitSpec
    {
        public bool enabled;
        public Curve speed, drag;
        public float dampen;
    }

    [Serializable] public class ForceSpec
    {
        public bool enabled, world, random_per_frame;
        public Curve x, y, z;
    }

    [Serializable] public class NoiseSpec
    {
        public bool enabled, damping = true;
        public Curve strength, scroll, position, rotation, size;
        public float frequency = 0.5f;
        public int octaves = 1, quality = 2;
    }

    [Serializable] public class ColourLife { public bool enabled; public Grad gradient = new Grad(); }
    [Serializable] public class CurveLife { public bool enabled; public Curve curve = new Curve(); }

    [Serializable] public class SheetSpec
    {
        public bool enabled, single_row, random_row = true;
        public int tiles_x = 1, tiles_y = 1;
        public string time = "lifetime";
        public float fps = 30, cycles = 1;
        public Curve frame, start_frame;
    }

    [Serializable] public class TrailSpec
    {
        public bool enabled, world, inherit_colour = true, die_with_particles = true, size_affects_width = true;
        public string mode = "per_particle";
        public float ratio = 1, min_vertex_distance = 0.2f;
        public int texture_mode;
        public Curve lifetime, width;
        public Grad colour_life, colour_trail;
    }

    [Serializable] public class CollisionSpec
    {
        public bool enabled, send_messages;
        public string type = "world";
        public Curve dampen, bounce, lifetime_loss;
        public float radius_scale = 1;
        public int quality;
    }

    [Serializable] public class SubSpec
    {
        public string type = "death", emitter = "";
        public int inherit;
        public float probability = 1;
    }

    [Serializable] public class CustomSpec
    {
        public string mode = "none";
        public Grad colour = new Grad();
        public Curve[] vector = new Curve[0];
    }

    [Serializable] public class RenderSpec
    {
        public string mode = "billboard", material = "", trail_material = "", mesh = "", sort = "none", alignment = "view";
        public float fudge, min_size, max_size = 0.5f, length_scale = 2, speed_scale, camera_speed_scale;
        public int order;
        public float[] pivot = { 0, 0, 0 }, flip = { 0, 0, 0 };
        public string[] streams = new string[0];
        public bool shadows;
    }

    [Serializable] public class SystemSpec
    {
        public string name = "system", parent = "", space = "local", scaling = "local", stop_action = "none";
        public float[] position = { 0, 0, 0 }, euler = { 0, 0, 0 }, scale = { 1, 1, 1 };
        public float duration = 3, sim_speed = 1, flip_rotation;
        public bool loop, prewarm, play_on_awake = true, size3d;
        public int max_particles = 1000, seed;
        public Curve delay, lifetime, speed, size, size_y, rotation, gravity;
        public Grad colour;
        public ShapeSpec shape;
        public EmissionSpec emission;
        public VelocitySpec velocity;
        public LimitSpec limit;
        public ForceSpec force;
        public NoiseSpec noise;
        public ColourLife colour_life;
        public CurveLife size_life, rotation_life;
        public SheetSpec sheet;
        public TrailSpec trail;
        public CollisionSpec collision;
        public SubSpec[] sub_emitters = new SubSpec[0];
        public CustomSpec custom1, custom2;
        public RenderSpec renderer;
    }

    [Serializable] public class TextureSpec
    {
        public string name, file, wrap = "clamp";
        public bool srgb = true, point, mips = true;
        public int max_size = 512;
    }

    [Serializable] public class FloatProp { public string k; public float v; }
    [Serializable] public class ColourProp { public string k; public float[] v; }

    [Serializable] public class MaterialSpec
    {
        public string name, texture = "", game_shader = "", borrow = "", blend = "alpha";
        public string[] keywords = new string[0], placeholder_keywords = new string[0], streams = new string[0];
        public FloatProp[] floats = new FloatProp[0];
        public ColourProp[] colours = new ColourProp[0];
        public int queue = 3000, placeholder_mode = 2;
    }

    [Serializable] public class MeshSpec
    {
        public string name, kind = "chunk";
        public int seed, detail = 1;
        public float[] size = { 1, 1, 1 };
    }

    [Serializable] public class FlickerSpec
    {
        public bool enabled;
        public float intensity = 0.1f, speed = 10, movement = 0.1f, ttl, fade = 0.2f, fade_in;
    }

    [Serializable] public class LodSpec { public bool enabled = true; public float distance = 40, shadow_distance = 20; }

    [Serializable] public class LightSpec
    {
        public string name = "light", parent = "", shadows = "none";
        public float[] position = { 0, 0, 0 }, colour = { 1, 1, 1 };
        public float intensity = 1, range = 5;
        public FlickerSpec flicker = new FlickerSpec();
        public LodSpec lod = new LodSpec();
    }

    [Serializable] public class ShakeSpec
    {
        public bool enabled, continuous, local_only;
        public float strength = 1, range = 20, delay, continuous_duration;
    }

    [Serializable] public class PreviewSpec
    {
        public float seconds = 3, distance = 6, target_height = 1, yaw = 35, pitch = 14, warmup, repeat, lift;
        public int fps = 30, width = 640, height = 360;
        public string[] references = new string[0];
        public float[] reference_offset = { 3, 0, 0 }, sheet_times = new float[0], figure_offset = new float[0];
        public bool figure = true;
    }

    [Serializable] public class EffectSpec
    {
        public string name, category = "";
        public TextureSpec[] textures = new TextureSpec[0];
        public MaterialSpec[] materials = new MaterialSpec[0];
        public MeshSpec[] meshes = new MeshSpec[0];
        public SystemSpec[] systems = new SystemSpec[0];
        public LightSpec[] lights = new LightSpec[0];
        public float timeout;
        public ShakeSpec shake = new ShakeSpec();
        public PreviewSpec preview = new PreviewSpec();

        public static EffectSpec Load(string path) =>
            JsonUtility.FromJson<EffectSpec>(File.ReadAllText(path)) ?? throw new InvalidDataException("empty spec " + path);
    }
#pragma warning restore IDE1006
}

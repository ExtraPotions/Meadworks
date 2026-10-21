// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// One shell of a Nightfall tornado's funnel, drawn as a mesh rather than particles, so the funnel reads as one
    /// churning column of cloud rather than swirling lines: rings of vertices up its height, each at the funnel's radius
    /// there (times its look's width), its axis bent with the same sway and trail as the particles round it
    /// (<see cref="TornadoSwirl.Bend"/>). It turns by sliding its cloud texture round the axis - fastest at the foot -
    /// twisted up its height so its bands spiral, and the cloud climbs as it turns. It fades in at its foot and out into
    /// the sky at its top, and its edges soften wherever its surface turns away from the camera, so it never shows a
    /// hard cone's outline. A lightning flash inside it lights it a cold blue. Built once with the tornado and reused;
    /// it allocates nothing as it runs.
    /// </summary>
    internal sealed class TornadoCone
    {
        /// <summary>The dense inner column.</summary>
        public static readonly Look Core = new Look
        {
            Width = 0.8f, Alpha = 0.45f, SpinFoot = 9f, SpinTop = 3.75f, Climb = 0.35f,
            Tint = new Color(0.30f, 0.31f, 0.34f),
        };

        /// <summary>The thinner, paler cloud wrapped round it, turning slower.</summary>
        public static readonly Look Veil = new Look
        {
            Width = 1.15f, Alpha = 0.18f, SpinFoot = 6f, SpinTop = 2.5f, Climb = 0.2f,
            Tint = new Color(0.42f, 0.43f, 0.47f),
        };

        private const int Rings = 16;
        private const int Segments = 32;
        private const int Row = Segments + 1;
        private const float Tau = Mathf.PI * 2f;

        /// <summary>How many times the cloud goes round, and up, the cone; how far its bands twist from foot to top.</summary>
        private const float RoundTiles = 2f;
        private const float UpTiles = 1.5f;
        private const float Twist = 0.6f;

        /// <summary>How its edge fades where the surface turns away from the camera: lower is a softer edge.</summary>
        private const float EdgeFade = 0.75f;

        private static readonly float[] Cos = Table(true);
        private static readonly float[] Sin = Table(false);

        private readonly Look _look;
        private readonly Mesh _mesh = new Mesh();
        private readonly Vector3[] _vertices = new Vector3[Rings * Row];
        private readonly Vector2[] _uvs = new Vector2[Rings * Row];
        private readonly Color32[] _colours = new Color32[Rings * Row];
        private float _turnFoot;
        private float _turnTop;
        private float _rise;

        public TornadoCone(Transform root, Look look, Material? material)
        {
            _look = look;
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colours);
            _mesh.SetTriangles(Triangles(), 0);
            GameObject holder = new GameObject("ecr_tornado_cone");
            holder.transform.SetParent(root, false);
            holder.AddComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// Each frame: lays the cone on the funnel <paramref name="grow"/> of the way formed and <paramref name="fade"/>
        /// of the way left, its axis bent by <paramref name="lean"/> at <paramref name="time"/>, softened toward
        /// <paramref name="eye"/> (the camera, in the tornado's own space).
        /// </summary>
        public void Drive(TornadoShape shape, float grow, float fade, float flash, Vector3 lean, float time, float dt,
            Vector3 eye)
        {
            _turnFoot += _look.SpinFoot * dt / Tau;
            _turnTop += _look.SpinTop * dt / Tau;
            _rise += _look.Climb * dt;
            Frame frame = new Frame(shape, lean, time, eye, shape.Height * Mathf.Lerp(TornadoSwirl.SeedHeight, 1f, grow),
                _look.Width * Mathf.Lerp(TornadoSwirl.SeedWidth, 1f, grow) * (1f + TornadoSwirl.Spread * (1f - fade)),
                _look.Alpha * Mathf.Clamp01(TornadoSwirl.SeedAlpha + TornadoSwirl.AlphaGrowth * grow) * fade,
                Color.Lerp(_look.Tint, TornadoSwirl.FlashColour, flash * 0.6f));
            for (int ring = 0; ring < Rings; ring++)
            {
                Ring(ring, frame);
            }
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colours);
            _mesh.RecalculateBounds();
        }

        private void Ring(int ring, Frame frame)
        {
            float share = ring / (float)(Rings - 1);
            float y = share * frame.Height;
            Vector3 centre = TornadoSwirl.Bend(frame.Shape, y, frame.Lean, frame.Time) + Vector3.up * y;
            float radius = frame.Shape.RadiusAt(share) * frame.Widen;
            Vector3 toEye = frame.Eye - centre;
            toEye.y = 0f;
            toEye = toEye.sqrMagnitude > 0.01f ? toEye.normalized : Vector3.forward;
            float turn = Mathf.Lerp(_turnFoot, _turnTop, share) + share * Twist;
            float alpha = frame.Alpha * Mathf.Clamp01(share / 0.08f) * Mathf.Clamp01((1f - share) / 0.25f);
            for (int seg = 0; seg < Row; seg++)
            {
                int i = ring * Row + seg;
                _vertices[i] = centre + new Vector3(Cos[seg] * radius, 0f, Sin[seg] * radius);
                _uvs[i] = new Vector2(seg / (float)Segments * RoundTiles - turn, share * UpTiles - _rise);
                float facing = Mathf.Abs(Cos[seg] * toEye.x + Sin[seg] * toEye.z);
                Color colour = frame.Tint;
                colour.a = alpha * Mathf.Pow(facing, EdgeFade);
                _colours[i] = colour;
            }
        }

        // Two triangles for each quad between one ring and the next; both sides are drawn.
        private static int[] Triangles()
        {
            int[] triangles = new int[(Rings - 1) * Segments * 6];
            int t = 0;
            for (int ring = 0; ring < Rings - 1; ring++)
            {
                for (int seg = 0; seg < Segments; seg++)
                {
                    int a = ring * Row + seg;
                    int c = a + Row;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = c;
                    triangles[t++] = c + 1;
                }
            }
            return triangles;
        }

        private static float[] Table(bool cos)
        {
            float[] table = new float[Row];
            for (int seg = 0; seg < Row; seg++)
            {
                float angle = seg / (float)Segments * Tau;
                table[seg] = cos ? Mathf.Cos(angle) : Mathf.Sin(angle);
            }
            return table;
        }

        /// <summary>One cone's look: its width against the funnel's, how solid, how fast it turns at its foot and top
        /// (radians a second), how fast its cloud climbs (tiles a second) and its grey.</summary>
        public sealed class Look
        {
            public float Width;
            public float Alpha;
            public float SpinFoot;
            public float SpinTop;
            public float Climb;
            public Color Tint;
        }

        /// <summary>What every ring of one frame shares.</summary>
        private readonly struct Frame
        {
            public readonly TornadoShape Shape;
            public readonly Vector3 Lean;
            public readonly float Time;
            public readonly Vector3 Eye;
            public readonly float Height;
            public readonly float Widen;
            public readonly float Alpha;
            public readonly Color Tint;

            public Frame(TornadoShape shape, Vector3 lean, float time, Vector3 eye, float height, float widen, float alpha,
                Color tint)
            {
                Shape = shape;
                Lean = lean;
                Time = time;
                Eye = eye;
                Height = height;
                Widen = widen;
                Alpha = alpha;
                Tint = tint;
            }
        }
    }
}

// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The broken axe's pieces, local on each peer (the preview's AssetWorkshop assets/ecp_headsman/blender_shatter.py,
    /// in the game): the axe's 29 pieces start where the axe broke, fly apart with a little of its speed and a burst of
    /// their own, fall, bounce and lie; just before the skeleton begins to form they lift, in their own colour, into a
    /// swirl round the spot; one at a time, from half a second into the forming, each darts into the skeleton where a
    /// bone is just appearing (<see cref="HeadsmanRising.BoneAt"/>) and is gone. With no skeleton raised they lie a
    /// while and sink.
    /// </summary>
    public sealed class HeadsmanShards
    {
        private sealed class Piece
        {
            public Transform Body = null!;
            public Vector3 At, Velocity, Spin, Lying;
            public float Lift, Landing, Radius, Height, Rate, Angle;
        }

        private const float Strike = 0.17f, Gravity = 9.8f;
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly Vector3 spot;

        public HeadsmanShards(Transform broken, Vector3 floor, int seed)
        {
            spot = floor;
            var random = new System.Random(seed);
            Transform[] parts = HeadsmanKit.Axe!.Cast<Transform>().ToArray();
            float[] landings = Landings(parts.Length, random);
            for (int i = 0; i < parts.Length; i++)
            {
                pieces.Add(Make(parts[i], broken, landings[i], random));
            }
        }

        /// <summary>One piece at a time in a random order, from the forming's hold to just before it is done.</summary>
        private static float[] Landings(int count, System.Random random)
        {
            float first = HeadsmanShatter.FormAfter + HeadsmanMoves.Hold, last = HeadsmanShatter.FormAfter + HeadsmanMoves.Forming - 0.1f;
            int[] order = Enumerable.Range(0, count).OrderBy(_ => random.Next()).ToArray();
            var at = new float[count];
            for (int k = 0; k < count; k++)
            {
                at[order[k]] = first + k * (last - first) / Mathf.Max(1, count - 1);
            }
            return at;
        }

        private Piece Make(Transform part, Transform broken, float landing, System.Random random)
        {
            float R() => (float)random.NextDouble();
            Vector3 middle = part.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            // The axe as it struck: its haft's middle where it broke, upright, the blade the way it flew.
            Quaternion axe = broken.rotation * Quaternion.Euler(0f, -90f, 0f);
            var pivot = new GameObject("ecp_headsman_shard").transform;
            pivot.SetPositionAndRotation(broken.position + axe * ((middle - HeadsmanAxe.Spine(0.75f)) * HeadsmanCreature.Size), axe);
            Transform body = Object.Instantiate(part.gameObject, pivot, false).transform;
            (body.localPosition, body.localRotation, body.localScale) = (-middle * HeadsmanCreature.Size, Quaternion.identity, Vector3.one * HeadsmanCreature.Size);
            Vector3 burst = new Vector3(R() * 2f - 1f, 0f, R() * 2f - 1f).normalized * (1.5f + 2f * R()) + Vector3.up * (1.5f + 2f * R());
            return new Piece
            {
                Body = pivot, At = pivot.position, Velocity = broken.forward * 2f + burst, Spin = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 1400f,
                Lift = HeadsmanShatter.FormAfter - 0.27f + 0.2f * R(), Landing = landing,
                Radius = (0.7f + 0.4f * R()) * HeadsmanCreature.Size, Height = (0.25f + 1.65f * R()) * HeadsmanCreature.Size, Rate = 3.2f + 1.2f * R(),
            };
        }

        /// <summary>Every piece at `since` seconds after the axe broke; `rising` the skeleton raised there, once it exists.</summary>
        public void Step(float since, HeadsmanRising? rising)
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            foreach (Piece piece in pieces)
            {
                if (since < piece.Lift || rising == null)
                {
                    Fall(piece, dt, rising == null && since > HeadsmanShatter.FormAfter + 3f ? since - HeadsmanShatter.FormAfter - 3f : 0f);
                }
                else if (since < piece.Landing - Strike)
                {
                    Swirl(piece, since - piece.Lift, dt);
                }
                else
                {
                    Dart(piece, since, rising);
                }
            }
        }

        /// <summary>Falling, bouncing off the floor with most of its speed gone, lying; sinking by `sink` seconds in.</summary>
        private void Fall(Piece piece, float dt, float sink)
        {
            float rest = spot.y + 0.03f;
            if (piece.At.y > rest + 1e-3f || piece.Velocity.sqrMagnitude > 0.16f)
            {
                piece.Velocity += Vector3.down * Gravity * dt;
                piece.At += piece.Velocity * dt;
                piece.Body.rotation = Quaternion.Euler(piece.Spin * dt) * piece.Body.rotation;
                if (piece.At.y < rest)
                {
                    piece.At.y = rest;
                    (piece.Velocity, piece.Spin) = (new Vector3(piece.Velocity.x * 0.45f, -piece.Velocity.y * 0.3f, piece.Velocity.z * 0.45f), piece.Spin * 0.5f);
                }
            }
            piece.Lying = piece.At;
            piece.Body.position = piece.At + Vector3.down * Mathf.Clamp01(sink / 2f) * 0.3f;
            piece.Body.gameObject.SetActive(sink < 2f);
        }

        /// <summary>Lifting from where it lay into a circle round the skeleton's spot, tightening as it turns.</summary>
        private void Swirl(Piece piece, float t, float dt)
        {
            if (t < dt * 1.5f)
            {
                piece.Angle = Mathf.Atan2(piece.Lying.z - spot.z, piece.Lying.x - spot.x);
            }
            float r = piece.Radius * (1f - 0.3f * Mathf.Min(1f, t / 2f)), a = piece.Angle + piece.Rate * t;
            Vector3 orbit = spot + new Vector3(r * Mathf.Cos(a), piece.Height + 0.08f * Mathf.Sin(5f * t + piece.Angle), r * Mathf.Sin(a));
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t / 0.5f));
            piece.At = Vector3.Lerp(piece.Lying, orbit, s) + Vector3.up * (0.3f * Mathf.Sin(Mathf.PI * s));
            piece.Body.position = piece.At;
            piece.Body.rotation = Quaternion.AngleAxis(240f * dt, piece.Spin.normalized) * piece.Body.rotation;
            piece.Body.gameObject.SetActive(true);
        }

        /// <summary>Darting from its orbit into the bone appearing as it lands, shrinking a little, then gone.</summary>
        private static void Dart(Piece piece, float since, HeadsmanRising rising)
        {
            float u = Mathf.Clamp01((since - (piece.Landing - Strike)) / Strike);
            Vector3 target = rising.BoneAt(piece.Landing - HeadsmanShatter.FormAfter);
            piece.Body.position = Vector3.Lerp(piece.At, target, u * u);
            piece.Body.localScale = Vector3.one * (1f - 0.3f * u);
            piece.Body.gameObject.SetActive(u < 1f);
        }

        /// <summary>The pieces go with the shatter.</summary>
        public void Clear()
        {
            foreach (Piece piece in pieces.Where(p => p.Body != null))
            {
                Object.Destroy(piece.Body.gameObject);
            }
            pieces.Clear();
        }
    }
}

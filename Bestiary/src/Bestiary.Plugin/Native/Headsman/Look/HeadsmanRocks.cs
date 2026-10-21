// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// Stones for the Executioner's slam, drawn on every peer and never networked: chunks thrown up where the axe lands,
    /// which fall, lie and sink. A pool of faceted chunks wearing the game's Black Forest rock.
    /// </summary>
    public sealed class HeadsmanRocks : MonoBehaviour
    {
        private sealed class Rock
        {
            public Transform Body = null!;
            public float Start = -1f, Size, Floor;
            public Vector3 At, Velocity, Spin;
        }

        private const int Count = 36;
        private const float Sink = 0.35f;

        /// <summary>The stones' material: the game's forest rock, set when the prefabs are built.</summary>
        public static Material? Stone;

        private static HeadsmanRocks? instance;
        private readonly List<Rock> rocks = new List<Rock>();
        private readonly System.Random random = new System.Random(11);

        private static HeadsmanRocks? Instance => instance != null ? instance : Stone == null ? null : instance = Create();

        /// <summary>Twelve chunks thrown up where a blow lands.</summary>
        public static void Burst(Vector3 at)
        {
            HeadsmanRocks? rocks = Instance;
            for (int i = 0; i < 12 && rocks != null; i++)
            {
                rocks.Throw(at);
            }
        }

        private static HeadsmanRocks Create()
        {
            var holder = new GameObject("ECP_Headsman_rocks");
            DontDestroyOnLoad(holder);
            HeadsmanRocks made = holder.AddComponent<HeadsmanRocks>();
            Mesh[] shapes = { HeadsmanChunk.Mesh(1), HeadsmanChunk.Mesh(2), HeadsmanChunk.Mesh(3), HeadsmanChunk.Mesh(4) };
            for (int i = 0; i < Count; i++)
            {
                var body = new GameObject("rock", typeof(MeshFilter), typeof(MeshRenderer));
                body.transform.SetParent(holder.transform, false);
                body.GetComponent<MeshFilter>().sharedMesh = shapes[i % shapes.Length];
                body.GetComponent<MeshRenderer>().sharedMaterial = Stone;
                body.SetActive(false);
                made.rocks.Add(new Rock { Body = body.transform });
            }
            return made;
        }

        private void Throw(Vector3 at)
        {
            Rock? rock = Free();
            if (rock == null)
            {
                return;
            }
            float angle = (float)random.NextDouble() * 360f;
            (rock.Start, rock.At, rock.Size, rock.Floor) = (Time.time, at + Vector3.up * 0.05f, 0.1f + 0.12f * (float)random.NextDouble(), at.y);
            rock.Velocity = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 3f + 2.5f * (float)random.NextDouble(), 1f + 2f * (float)random.NextDouble());
            rock.Spin = new Vector3(Jitter(), Jitter(), Jitter()) * 900f;
        }

        private void Update()
        {
            foreach (Rock rock in rocks)
            {
                if (rock.Start >= 0f)
                {
                    Fly(rock, Time.time - rock.Start, Time.deltaTime);
                }
            }
        }

        /// <summary>A chunk flying and falling, lying where it lands, sinking after a while.</summary>
        private void Fly(Rock rock, float since, float dt)
        {
            rock.Body.gameObject.SetActive(true);
            float rest = rock.Floor + rock.Size * 0.3f;
            if (rock.Velocity.y > 0f || rock.At.y > rest)
            {
                rock.Velocity += Vector3.down * 9.8f * dt;
                rock.At += rock.Velocity * dt;
                rock.Body.rotation = Quaternion.Euler(rock.Spin * dt) * rock.Body.rotation;
            }
            rock.At.y = Mathf.Max(rock.At.y, rest);
            float sink = Mathf.Clamp01((since - 1.4f) / Sink);
            rock.Body.position = rock.At + Vector3.down * (sink * rock.Size);
            rock.Body.localScale = Vector3.one * rock.Size;
            if (sink >= 1f || since > 3f)
            {
                Retire(rock);
            }
        }

        private static void Retire(Rock rock)
        {
            rock.Start = -1f;
            rock.Body.gameObject.SetActive(false);
        }

        private Rock? Free() => rocks.Find(r => r.Start < 0f);

        private float Jitter() => (float)random.NextDouble() * 2f - 1f;
    }
}

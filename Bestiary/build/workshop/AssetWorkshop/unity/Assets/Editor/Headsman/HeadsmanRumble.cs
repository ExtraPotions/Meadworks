using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// Ground rocks for the preview: the scrape's shockwave (<see cref="Wave"/>, what hits in the mod) pushes slabs up out
    /// of the ground in a row that runs outward from where the scraping edge passed at <see cref="WaveSpeed"/> to
    /// <see cref="WaveReach"/>, each jutting up leaning outward a moment later the further out it is, holding and
    /// sinking again; a burst (<see cref="Burst"/>) throws chunks up where the slam lands, which fall, lie and sink.
    /// </summary>
    public sealed class HeadsmanRumble
    {
        private sealed class Rock
        {
            public Transform Body;
            public float Start = -1f, Size;
            public bool Thrown;
            public Vector3 At, Velocity, Spin;
        }

        public const float WaveSpeed = 8f, WaveReach = 3.2f;
        private const float Up = 0.09f, Hold = 0.3f, Sink = 0.35f;
        private readonly List<Rock> rocks = new List<Rock>();
        private readonly System.Random random = new System.Random(11);

        public HeadsmanRumble(int count)
        {
            var stone = HeadsmanProps.Plain("rock_stone", new Color(0.085f, 0.08f, 0.07f));
            for (int i = 0; i < count; i++)
            {
                GameObject chunk = HeadsmanProps.Chunk($"rock_{i:000}", stone, new Vector3(0.8f, 0.5f, 1.15f), 300 + i);
                chunk.SetActive(false);
                rocks.Add(new Rock { Body = chunk.transform });
            }
        }

        public IEnumerable<Renderer> Renderers => rocks.ConvertAll(r => r.Body.GetComponent<Renderer>());

        /// <summary>A row of rocks rising outward from `edge` (away from `centre`), each later by its distance.</summary>
        public void Wave(Vector3 edge, Vector3 centre, float now)
        {
            Vector3 outward = Vector3.ProjectOnPlane(edge - centre, Vector3.up).normalized;
            Vector3 ground = new Vector3(edge.x, 0f, edge.z);
            for (float d = 0.15f; d <= WaveReach; d += 0.32f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, outward) * (Jitter() * 0.25f);
                Pop(ground + outward * d + side, outward, now + d / WaveSpeed, Mathf.Lerp(0.6f, 0.3f, d / WaveReach));
            }
        }

        /// <summary>Chunks thrown up where a strike lands.</summary>
        public void Burst(Vector3 at, float now)
        {
            for (int i = 0; i < 12; i++)
            {
                Rock rock = Free();
                if (rock == null)
                    return;
                float angle = (float)random.NextDouble() * 360f;
                rock.Thrown = true;
                (rock.Start, rock.At, rock.Size) = (now, new Vector3(at.x, 0.05f, at.z), 0.1f + 0.12f * (float)random.NextDouble());
                rock.Velocity = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 3f + 2.5f * (float)random.NextDouble(), 1f + 2f * (float)random.NextDouble());
                rock.Spin = new Vector3(Jitter(), Jitter(), Jitter()) * 900f;
            }
        }

        public void Update(float now, float dt)
        {
            foreach (Rock rock in rocks)
            {
                if (rock.Start < 0f)
                    continue;
                if (rock.Thrown)
                    Fly(rock, now, dt);
                else
                    Rise(rock, now);
            }
        }

        /// <summary>A slab jutting up out of the ground, leaning outward the way the wave runs.</summary>
        private void Pop(Vector3 at, Vector3 outward, float start, float size)
        {
            Rock rock = Free();
            if (rock == null)
                return;
            (rock.Thrown, rock.Start, rock.At, rock.Size) = (false, start, at, size * (0.8f + 0.4f * (float)random.NextDouble()));
            Vector3 lean = Quaternion.AngleAxis(Jitter() * 25f, Vector3.up) * Vector3.Lerp(Vector3.up, outward, 0.3f + 0.15f * Jitter());
            rock.Body.rotation = Quaternion.LookRotation(lean, outward) * Quaternion.Euler(0f, 0f, Jitter() * 40f);
        }

        private void Rise(Rock rock, float now)
        {
            float since = now - rock.Start;
            rock.Body.gameObject.SetActive(since >= 0f);
            if (since < 0f)
                return;
            float up = since < Up ? since / Up : since < Up + Hold ? 1f : 1f - (since - Up - Hold) / Sink;
            if (up <= 0f)
                Retire(rock);
            float shake = since < Up + Hold ? 0.015f * Mathf.Sin(since * 90f) : 0f;
            rock.Body.position = rock.At + Vector3.up * (rock.Size * (0.8f * Mathf.Clamp01(up) - 0.55f) + shake);
            rock.Body.localScale = Vector3.one * rock.Size;
        }

        private void Fly(Rock rock, float now, float dt)
        {
            float since = now - rock.Start;
            rock.Body.gameObject.SetActive(true);
            if (rock.At.y > rock.Size * 0.3f || rock.Velocity.y > 0f)
            {
                rock.Velocity += Vector3.down * 9.8f * dt;
                rock.At += rock.Velocity * dt;
                rock.Body.rotation = Quaternion.Euler(rock.Spin * dt) * rock.Body.rotation;
            }
            rock.At.y = Mathf.Max(rock.At.y, rock.Size * 0.3f);
            float sink = Mathf.Clamp01((since - 1.4f) / Sink);
            rock.Body.position = rock.At + Vector3.down * (sink * rock.Size);
            rock.Body.localScale = Vector3.one * rock.Size;
            if (sink >= 1f)
                Retire(rock);
        }

        private static void Retire(Rock rock)
        {
            rock.Start = -1f;
            rock.Body.gameObject.SetActive(false);
        }

        private Rock Free() => rocks.Find(r => r.Start < 0f);

        private float Jitter() => (float)random.NextDouble() * 2f - 1f;
    }
}

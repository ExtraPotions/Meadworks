using System;
using System.Collections.Generic;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// Something forming out of nothing, for the preview (the new axe in the raised hands, a skeleton where a thrown axe
    /// broke): glowing motes spiral in from all round onto points of the thing's shape again and again while it forms,
    /// a light swells at its heart (a part named light_*: Blender hangs a point light on it and never draws the part
    /// itself), a glowing ring turns on the ground under it and closes in, and when it is solid the motes burst out,
    /// the light flashes and the ring spreads and fades.
    /// </summary>
    public sealed class HeadsmanForming
    {
        private sealed class Mote
        {
            public Transform Body;
            public float Delay, Duration, Angle, Height, Along;
            public Vector3 Burst;
        }

        public const float BurstTime = 0.35f;
        private readonly List<Mote> motes = new List<Mote>();
        private readonly Transform orb, ring;
        private readonly float ringSize;

        public HeadsmanForming(string name, int count, int seed, float ringSize)
        {
            this.ringSize = ringSize;
            var random = new System.Random(seed);
            Material glow = HeadsmanProps.Plain(name + "_glow", new Color(0.45f, 1f, 0.72f), true);
            for (int i = 0; i < count; i++)
            {
                GameObject chunk = HeadsmanProps.Chunk($"{name}_mote_{i:00}", glow, Vector3.one * 0.1f, seed * 100 + i);
                chunk.SetActive(false);
                float Next() => (float)random.NextDouble();
                motes.Add(new Mote
                {
                    Body = chunk.transform, Delay = 0.35f * Next(), Duration = 0.7f + 0.4f * Next(), Angle = 360f * Next(),
                    Height = 0.4f + 0.9f * Next(), Along = Next(),
                    Burst = Quaternion.Euler(-60f * Next(), 360f * Next(), 0f) * Vector3.forward * (2.5f + 2f * Next()),
                });
            }
            orb = HeadsmanProps.Primitive(PrimitiveType.Sphere, "light_" + name, glow).transform;
            ring = HeadsmanProps.Ring(name + "_ring", glow, 1f, 0.08f).transform;
            Show(false);
        }

        public IEnumerable<Renderer> Renderers
        {
            get
            {
                foreach (Mote mote in motes)
                    yield return mote.Body.GetComponent<Renderer>();
                yield return orb.GetComponent<Renderer>();
                yield return ring.GetComponent<Renderer>();
            }
        }

        public void Hide() => Show(false);

        /// <summary>
        /// `forming` seconds since it began to form, `span` how long forming takes; the motes stream onto
        /// `shape(0..1)`, the light sits at `heart`, the ring on the ground at `feet`.
        /// </summary>
        public void Update(float forming, float span, Func<float, Vector3> shape, Vector3 heart, Vector3 feet, float time)
        {
            float since = forming - span;
            bool on = forming >= 0f && since < BurstTime;
            Show(on);
            if (!on)
                return;
            foreach (Mote mote in motes)
                Place(mote, forming, since, shape(mote.Along));
            Glow(forming / span, since, time, heart, feet);
        }

        private void Show(bool on)
        {
            orb.gameObject.SetActive(on);
            ring.gameObject.SetActive(on);
            if (!on)
                foreach (Mote mote in motes)
                    mote.Body.gameObject.SetActive(false);
        }

        /// <summary>Spiralling in to its point until the thing is solid, then flung out and shrinking.</summary>
        private static void Place(Mote mote, float forming, float since, Vector3 target)
        {
            if (since >= 0f)
            {
                float gone = since / BurstTime;
                mote.Body.gameObject.SetActive(gone < 1f);
                mote.Body.position = target + mote.Burst * since;
                mote.Body.localScale = Vector3.one * (1.4f * (1f - gone));
                return;
            }
            // Each mote streams in again and again while the thing forms: in over its duration, a short rest, in again.
            float cycle = Mathf.Repeat(forming - mote.Delay, mote.Duration + 0.25f) / mote.Duration;
            float u = forming < mote.Delay ? 0f : Mathf.Min(cycle, 1f);
            float radius = 1.3f * Mathf.Pow(1f - u, 1.3f);
            float angle = (mote.Angle + 540f * u) * Mathf.Deg2Rad;
            mote.Body.gameObject.SetActive(forming >= mote.Delay && u < 0.98f);
            mote.Body.position = target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.up * (mote.Height * (1f - u));
            mote.Body.localScale = Vector3.one * (0.5f + u);
        }

        /// <summary>The light swelling as the thing forms and flashing when it is solid; the ring on the ground.</summary>
        private void Glow(float progress, float since, float time, Vector3 heart, Vector3 feet)
        {
            float pulse = 1f + 0.12f * Mathf.Sin(time * 38f);
            float size = since < 0f ? (0.04f + 0.08f * progress) * pulse : 0.25f * (1f - since / BurstTime);
            orb.position = heart;
            orb.localScale = Vector3.one * Mathf.Max(size, 0.001f);
            float radius = ringSize * (since < 0f ? Mathf.Lerp(1.6f, 0.9f, progress) : Mathf.Lerp(0.9f, 2.6f, since / BurstTime));
            ring.SetPositionAndRotation(feet + Vector3.up * 0.02f, Quaternion.Euler(0f, time * 90f, 0f));
            ring.localScale = new Vector3(radius, since < 0f ? 1f : 1f - since / BurstTime, radius);
        }
    }
}

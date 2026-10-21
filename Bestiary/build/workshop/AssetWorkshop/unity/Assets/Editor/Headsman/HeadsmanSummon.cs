using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Workshop.Crossbow;

namespace Workshop.Headsman
{
    /// <summary>
    /// A new skeleton forming where a thrown axe broke, for the preview. The axe's own pieces scatter and lie on the
    /// ground (Blender makes and moves them, from the thrown axe's mesh: blender_shatter.py), then rise and fly into
    /// the shape of a skeleton; here the skeleton (the game's own Skeleton, a new one for each throw) forms in place in
    /// the same window as the new axe (<see cref="HeadsmanForming"/>: a light at its chest, a ring under it, no motes;
    /// Blender fades its bones in one at a time from the feet up, blender_reveal.py) and then stands idling. Each summon is recorded
    /// for Blender: the frame of the hit, when it begins to form and when it is solid, where it stands and points along
    /// its bones for the pieces to fly to.
    /// </summary>
    public sealed class HeadsmanSummon
    {
        public sealed class Record
        {
            public int Index, Hit, Form = -1, Solid = -1;
            public Vector3 Spot;
            public Vector3[] Targets;
        }


        private static readonly string[] Bones =
        {
            "Head", "Neck", "Spine2", "Spine1", "Spine", "Hips", "LeftArm", "LeftForeArm", "LeftHand", "RightArm",
            "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot",
        };

        private readonly List<(GameObject body, XbowPoser poser)> skeletons = new List<(GameObject, XbowPoser)>();
        private readonly List<(XbowPoser poser, float since)> standing = new List<(XbowPoser, float)>();
        private readonly HeadsmanForming forming = new HeadsmanForming("summon", 0, 9, 0.8f);
        private Record current;
        private float formAt, solidAt;

        public readonly List<Record> Records = new List<Record>();
        public readonly List<string> Cues = new List<string>();

        public HeadsmanSummon(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject skeleton = XbowReference.Skeleton();
                skeleton.name = $"summoned_skeleton_{i}";
                skeleton.SetActive(false);
                skeletons.Add((skeleton, new XbowPoser(skeleton)));
            }
        }

        public IEnumerable<Renderer> Renderers => forming.Renderers;

        /// <summary>Whether a skeleton is still to form or forming.</summary>
        public bool Busy => current != null;

        /// <summary>Every summoned skeleton's renderers, in order (the point caches).</summary>
        public IEnumerable<Renderer> Skeletons => skeletons.SelectMany(s => s.body.GetComponentsInChildren<Renderer>(true));

        /// <summary>The body renderer of summon `index` (Blender dresses it as a ghost while it forms).</summary>
        public Renderer Body(int index) => skeletons[index].body.GetComponentInChildren<SkinnedMeshRenderer>(true);

        /// <summary>
        /// The axe broke at `hit`; the next skeleton forms at `spot`, facing `facing` degrees, from `form` until
        /// `solid` (seconds on the same clock as `now`: the window the boss's new axe forms in).
        /// </summary>
        public void Burst(Vector3 hit, Vector3 spot, float facing, (float now, float form, float solid) at, int frame)
        {
            int index = Records.Count + (current == null ? 0 : 1);
            if (index >= skeletons.Count)
                return;
            skeletons[index].body.transform.SetPositionAndRotation(spot, Quaternion.Euler(0f, facing, 0f));
            current = new Record { Index = index, Hit = frame, Spot = Blender(spot) };
            (formAt, solidAt) = (at.form, at.solid);
        }

        public void Update(float now, int frame)
        {
            foreach (var (poser, since) in standing)
                poser.Pose("Idle", Mathf.Repeat(now - since, poser.Length("Idle")));
            if (current == null)
            {
                forming.Hide();
                return;
            }
            float into = now - formAt, span = solidAt - formAt;
            if (into >= 0f && current.Form < 0)
                Begin(frame);
            if (current.Form < 0)
                return;
            var (body, idle) = skeletons[current.Index];
            idle.Pose("Idle", Mathf.Repeat(into, idle.Length("Idle")));
            forming.Update(into, span, Along, Bone("Spine2"), body.transform.position, now);
            if (into >= span && current.Solid < 0)
            {
                current.Solid = frame;
            }
            if (into >= span + HeadsmanForming.BurstTime)
                Finish(now - into);
        }

        private void Begin(int frame)
        {
            var (body, poser) = skeletons[current.Index];
            body.SetActive(true);
            poser.Pose("Idle", 0f);
            current.Form = frame;
            current.Targets = Bones.Select(b => Blender(XbowReference.Bone(body, b).position)).ToArray();
        }

        private void Finish(float formedAt)
        {
            standing.Add((skeletons[current.Index].poser, formedAt));
            Records.Add(current);
            current = null;
            forming.Hide();
        }

        /// <summary>A point along the forming skeleton's bones, head to feet, for the motes.</summary>
        private Vector3 Along(float u)
        {
            float at = u * (Bones.Length - 1);
            int i = Mathf.Min((int)at, Bones.Length - 2);
            return Vector3.Lerp(Bone(Bones[i]), Bone(Bones[i + 1]), at - i);
        }

        private Vector3 Bone(string name) => XbowReference.Bone(skeletons[current.Index].body, name).position;

        /// <summary>Unity's (x, y, z) in Blender's axes.</summary>
        public static Vector3 Blender(Vector3 u) => new Vector3(-u.x, -u.z, u.y);
    }
}

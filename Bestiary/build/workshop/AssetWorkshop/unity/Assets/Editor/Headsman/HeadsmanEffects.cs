using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Workshop.Headsman
{
    /// <summary>
    /// Everything around the headsman in the preview bake, driven by the attack playing and its clip time: three stand-in
    /// targets (in front, far ahead for the throws, behind for the rear strike; the one in front steps away while the
    /// throws play, since they are for a target out of reach), the thrown axe and where it breaks into a new skeleton,
    /// the new axe forming, the rumble behind the scrape and the chunks thrown up by the slam.
    /// </summary>
    public sealed class HeadsmanEffects
    {
        public static readonly Vector3 FrontDummy = new Vector3(-0.15f, 0f, 1.3f);
        public static readonly Vector3 FarDummy = new Vector3(0f, 0f, 10f);
        public static readonly Vector3 RearDummy = new Vector3(0.2f, 0f, -1.45f);

        private readonly GameObject boss;
        private readonly HeadsmanPlayback playback;
        private readonly GameObject front, far, rear;
        private readonly HeadsmanFlight flight;
        private readonly HeadsmanSummon summon = new HeadsmanSummon(2);
        private readonly HeadsmanRumble rumble = new HeadsmanRumble(110);
        private readonly HeadsmanRegen regen;
        private readonly HeadsmanGrip grip;

        /// <summary>Sound cues the effects raised this frame (shatter, summon_form, summon_solid, debris); the timeline takes them.</summary>
        public readonly List<string> Cues = new List<string>();
        private readonly HeadsmanGhost ghost;
        private HeadsmanMove last;
        private float lastTime;
        private int frame, throws;
        private bool throwing;

        public HeadsmanEffects(GameObject boss, HeadsmanGrip grip, HeadsmanPlayback playback, float scale)
        {
            this.boss = boss;
            this.playback = playback;
            front = HeadsmanProps.Dummy("dummy_front", FrontDummy, 180f);
            far = HeadsmanProps.Dummy("dummy_far", FarDummy, 180f);
            rear = HeadsmanProps.Dummy("dummy_rear", RearDummy, 0f);
            flight = new HeadsmanFlight(scale);
            ghost = new HeadsmanGhost(boss, grip);
            regen = new HeadsmanRegen(boss);
            this.grip = grip;
        }

        /// <summary>Rigid renderers with the names Blender gives them.</summary>
        public IEnumerable<(Renderer renderer, string name)> Rigid()
        {
            yield return (playback.Axe.GetComponentInChildren<Renderer>(true), "axe_held");
            yield return (ghost.Renderers[0], "axe_ghost");
            yield return (flight.Renderers[0], "axe_thrown");
            foreach (Renderer renderer in summon.Renderers.Concat(rumble.Renderers).Concat(regen.Renderers))
                yield return (renderer, renderer.name);
            foreach (GameObject dummy in new[] { front, far, rear })
                foreach (Renderer renderer in dummy.GetComponentsInChildren<Renderer>(true))
                    yield return (renderer, renderer.name);
        }

        public IEnumerable<Renderer> Skinned => summon.Skeletons;

        /// <summary>The skeletons that formed where thrown axes broke, and the body renderer of each.</summary>
        public HeadsmanSummon Summon => summon;

        public void Update(HeadsmanMove move, float time, float now, float dt)
        {
            if (move != null && move != last)
                throwing = move.Flight != Flight.None;
            front.SetActive(!throwing);
            ghost.Update(move, time);
            Transform hand = Crossbow.XbowReference.Bone(boss, "RightHand");
            regen.Update(move, time, playback.Axe.transform, hand.TransformPoint(grip.Right.position));
            if (move != null)
                Events(move, move == last ? lastTime : -1f, time, now);
            if (flight.Update(now, out Vector3 hit))
                Land(hit, now, move, time);
            summon.Update(now, frame);
            Cues.AddRange(summon.Cues);
            summon.Cues.Clear();
            rumble.Update(now, dt);
            (last, lastTime) = (move, time);
            frame++;
        }

        private void Events(HeadsmanMove move, float from, float time, float now)
        {
            if (Crossed(from, time, move.Release))
                flight.Launch(playback.Axe.transform, FarDummy + Vector3.up * 1.2f, move.Flight, now);
            if (Crossed(from, time, move.Impact) && Edge().y < 0.4f)
                rumble.Burst(Edge(), now);
            if (time >= move.Scrape.x && time < move.Scrape.y && frame % 2 == 0)
                rumble.Wave(Edge(), boss.transform.position, now);
        }

        /// <summary>The thrown axe breaks on the far target; a skeleton will form beside it, turned to face it.</summary>
        private void Land(Vector3 hit, float now, HeadsmanMove move, float time)
        {
            Cues.Add("shatter");
            float side = throws++ % 2 == 0 ? -1.3f : 1.3f;
            // The skeleton forms in the same window as the boss's new axe (the throw's Ghost to Solid).
            var at = (now, now + move.Ghost - time, now + move.Solid - time);
            summon.Burst(hit, FarDummy + new Vector3(side, 0f, -0.4f), side < 0f ? 90f : -90f, at, frame);
        }

        private static bool Crossed(float from, float to, float at) => at >= 0f && from < at && to >= at;

        private Vector3 Edge() => playback.Axe.transform.TransformPoint(HeadsmanAxe.Edge);
    }
}

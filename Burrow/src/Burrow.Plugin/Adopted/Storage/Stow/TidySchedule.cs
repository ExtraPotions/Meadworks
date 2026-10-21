using System.Collections.Generic;
using System.Diagnostics;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// When Auto Tidy looks at a chest - only when there is a reason, never on a timer for every chest. A chest this
    /// client owns is looked at once when it is first seen (spread over <see cref="FirstSpread"/> seconds), a few
    /// seconds after its contents changed or it was closed, and, while strays in it found no home with room, again
    /// after 1, 2, 4, 8 and then every 10 minutes, or sooner when a chest near it changes (a home may have appeared or
    /// emptied). A quiet base costs a dictionary lookup per chest per second. The tick is the game's once-a-second
    /// <c>CheckForChanges</c> of every container; at most one look per frame, and a chest looks at most once every
    /// <see cref="MinGap"/> seconds. Looks are timed; <see cref="Stats"/> reports them for <c>burrow tidy</c>.
    /// </summary>
    public static class TidySchedule
    {
        private const float ChangeDelay = 3f;
        private const float FirstSpread = 30f;
        private const float MinGap = 10f;
        private static readonly float[] Retries = { 60f, 120f, 240f, 480f, 600f };

        private sealed class State
        {
            public float Next = float.MaxValue;
            public float LastLook = float.MinValue;
            public int Waits;
        }

        private static readonly Dictionary<Container, State> states = new Dictionary<Container, State>();
        private static readonly HashSet<Container> waiting = new HashSet<Container>();
        private static readonly PruneMark pruneMark = new PruneMark(512);
        private static int lastFrame = -1;

        public static int Looks { get; private set; }
        public static int StacksMoved { get; private set; }
        public static double TotalMs { get; private set; }
        public static double SlowestMs { get; private set; }

        /// <summary>After the container's <c>CheckForChanges</c> (<see cref="ContainerTickPatch"/>); <paramref name="__state"/> is the revision it had loaded before.</summary>
        public static void Tick(Container __instance, uint __state)
        {
            ZNetView view = __instance.m_nview;
            if (!On || view == null || !view.IsValid())
                return;
            if (!view.IsOwner())
            {
                if (__instance.m_lastRevision != __state && waiting.Count > 0)
                    Wake(__instance.transform.position);
                return;
            }
            if (Player.m_localPlayer == null || ZNet.instance == null)
                return;
            State state = StateOf(__instance);
            if (Time.time < state.Next || Time.frameCount == lastFrame)
                return;
            lastFrame = Time.frameCount;
            Look(__instance, state);
        }

        public static int Waiting => waiting.Count;

        /// <summary>The chest is looked at <paramref name="delay"/> seconds from now, unless its turn comes sooner.</summary>
        public static void Soon(Container chest, float delay = ChangeDelay)
        {
            State state = StateOf(chest);
            float at = Mathf.Max(Time.time + delay, state.LastLook + MinGap);
            if (at < state.Next)
                state.Next = at;
        }

        /// <summary>A chest's contents changed on this client: it is looked at soon, and so are waiting chests near it.</summary>
        public static void Changed(Container chest)
        {
            Soon(chest);
            Wake(chest.transform.position);
        }

        public static string Stats() =>
            $"{Looks} looks, {StacksMoved} stacks moved, {(Looks > 0 ? TotalMs / Looks : 0.0):0.000} ms on average, slowest {SlowestMs:0.000} ms, {waiting.Count} chests waiting for a home";

        private static bool On => StowSettings.Enabled.Value && StowSettings.AutoTidy.Value;

        private static void Look(Container chest, State state)
        {
            state.Next = float.MaxValue;
            long start = Stopwatch.GetTimestamp();
            bool looked = TidySweep.Run(chest, out int moved, out bool strays);
            double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            state.LastLook = Time.time;
            if (looked)
                Count(ms, moved);
            float again = Again(chest, state, looked, moved, strays);
            if (again < state.Next)
                state.Next = again;
        }

        /// <summary>When the chest is looked at next: soon after a full budget, later and later while strays wait, else on a change.</summary>
        private static float Again(Container chest, State state, bool looked, int moved, bool strays)
        {
            if (looked && moved >= TidySweep.MaxStacks)
                return Time.time + ChangeDelay;
            if (!looked || !strays)
            {
                state.Waits = 0;
                waiting.Remove(chest);
                return float.MaxValue;
            }
            waiting.Add(chest);
            float delay = Retries[Mathf.Min(state.Waits, Retries.Length - 1)];
            state.Waits++;
            return Time.time + delay;
        }

        private static void Count(double ms, int moved)
        {
            Looks++;
            StacksMoved += moved;
            TotalMs += ms;
            if (ms > SlowestMs)
                SlowestMs = ms;
            if (ms > 2.0)
                Plugin.Log.LogDebug($"Burrow: a tidy look took {ms:0.0} ms");
        }

        /// <summary>Waiting chests within <see cref="TidyChests.Range"/> of a change get a look soon.</summary>
        private static void Wake(Vector3 position)
        {
            List<Container> gone = null;
            foreach (Container chest in waiting)
            {
                if (chest == null)
                    (gone ?? (gone = new List<Container>())).Add(chest);
                else if (Vector3.Distance(position, chest.transform.position) <= TidyChests.Range)
                    Soon(chest);
            }
            gone?.ForEach(chest => waiting.Remove(chest));
        }

        private static State StateOf(Container chest)
        {
            if (states.TryGetValue(chest, out State state))
                return state;
            if (pruneMark.Due(states.Count))
                Prune();
            state = new State { Next = Time.time + FirstSpread * ((chest.GetInstanceID() & 0xFF) / 256f) };
            states[chest] = state;
            return state;
        }

        private static void Prune()
        {
            List<Container> gone = new List<Container>();
            foreach (Container key in states.Keys)
            {
                if (key == null)
                    gone.Add(key);
            }
            gone.ForEach(key => states.Remove(key));
            pruneMark.Pruned(states.Count);
        }
    }
}

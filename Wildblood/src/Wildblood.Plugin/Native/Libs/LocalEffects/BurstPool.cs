// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, ValheimModLibs, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Wildblood.AdoptedLibs.LocalEffects
{
    /// <summary>
    /// Spent one-shot bursts (<see cref="LocalEffect.Flash"/>, <see cref="LocalEffect.FlashWhole"/>,
    /// <see cref="LocalEffect.FlashScaled"/>) kept for the next burst of the same kind, so a fight does not make and
    /// destroy a copy per hit. A kept copy is hidden when its time is up, its particles cleared, and shown again where
    /// the next burst is wanted: its particle systems play from the start as they do in a fresh copy (play on awake
    /// runs when the object is shown again). Only a copy that restarts completely that way is kept, decided once per
    /// prefab from its parts: particle systems that do not stop themselves, their renderers, plain meshes, plain lights,
    /// colliders (switched off), a timer on the root and what <see cref="CloneParts.Strip"/> removes. Any other part acts once per life as
    /// it wakes and would not act again, so a copy with one is destroyed as before: the game's sound player (it plays
    /// once), LightLod and LightFlicker (they fade their light once), a camera shake, an animator, a trail (it would
    /// streak from the last place), a rigidbody, any script of the effect's own. The prefab's own timer is removed
    /// from a kept copy and its time kept instead. Sizing and thinning multiply the copy's own numbers once, when it is
    /// made, so a kept copy serves only the same prefab, sizing, radius, scale and density (rounded,
    /// <see cref="BurstSizes"/>). Never more than <see cref="KeptPerKind"/> copies of one kind wait, and never more
    /// than <see cref="KeptInAll"/> in all: past that the kind used longest ago goes (a density the player has since
    /// changed, a radius no longer drawn). Copies are scene objects and go with the scene.
    /// </summary>
    internal static class BurstPool
    {
        private const int KeptPerKind = 8;
        private const int KeptInAll = 48;

        private static readonly Type[] RestartableParts =
        {
            typeof(Transform), typeof(ParticleSystem), typeof(ParticleSystemRenderer), typeof(MeshFilter),
            typeof(MeshRenderer), typeof(Light), typeof(Collider), typeof(TimedDestruction),
            typeof(ZNetView), typeof(Aoe), typeof(Projectile), typeof(ZSyncTransform),
        };

        private static readonly Dictionary<BurstKey, Stack<GameObject>> Idle = new Dictionary<BurstKey, Stack<GameObject>>();
        private static readonly Dictionary<int, bool> Restartable = new Dictionary<int, bool>();
        private static readonly Dictionary<BurstKey, float> LastUsed = new Dictionary<BurstKey, float>();
        private static int kept;   // entries in Idle, destroyed ones (the scene went) included until popped

        /// <summary>A kept copy of this kind shown again at <paramref name="position"/>; false when none waits.</summary>
        public static bool Reuse(BurstKey key, Vector3 position)
        {
            if (!Idle.TryGetValue(key, out Stack<GameObject> idle))
            {
                return false;
            }
            LastUsed[key] = Time.time;
            while (idle.Count > 0)
            {
                GameObject copy = idle.Pop();
                kept--;
                PooledBurst? burst = copy != null ? copy.GetComponent<PooledBurst>() : null;
                if (burst != null)
                {
                    copy!.transform.SetPositionAndRotation(position, Quaternion.identity);
                    copy.SetActive(true);
                    burst.Live();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A fresh copy's end: kept for the next burst of its kind after <paramref name="life"/> seconds (or its own
        /// timer's, when shorter) when it restarts completely, otherwise destroyed then as before.
        /// </summary>
        public static void Release(GameObject prefab, GameObject clone, BurstKey key, float life)
        {
            if (!Restarts(prefab))
            {
                Object.Destroy(clone, life);
                return;
            }
            PooledBurst burst = clone.AddComponent<PooledBurst>();
            burst.Begin(key, Mathf.Min(life, OwnTimer(clone)), clone.GetComponentsInChildren<ParticleSystem>(true));
        }

        /// <summary>From a kept copy when its time is up: hidden and kept, or destroyed when enough of its kind wait.</summary>
        internal static void Spend(PooledBurst burst, ParticleSystem[] systems)
        {
            if (!Idle.TryGetValue(burst.Key, out Stack<GameObject> idle))
            {
                Idle[burst.Key] = idle = new Stack<GameObject>();
            }
            if (idle.Count >= KeptPerKind)
            {
                Object.Destroy(burst.gameObject);
                return;
            }
            if (kept >= KeptInAll && OldestKind(burst.Key, out BurstKey oldest))
            {
                Drop(oldest);
            }
            Hide(burst.gameObject, systems);
            idle.Push(burst.gameObject);
            kept++;
            LastUsed[burst.Key] = Time.time;
        }

        private static void Hide(GameObject copy, ParticleSystem[] systems)
        {
            foreach (ParticleSystem system in systems)
            {
                if (system != null)
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
            copy.SetActive(false);
        }

        /// <summary>The kind with copies waiting that was used longest ago, other than <paramref name="keep"/>.</summary>
        private static bool OldestKind(BurstKey keep, out BurstKey oldest)
        {
            oldest = keep;
            float oldestAt = float.PositiveInfinity;
            foreach (KeyValuePair<BurstKey, Stack<GameObject>> kind in Idle)
            {
                float at = LastUsed.TryGetValue(kind.Key, out float used) ? used : float.NegativeInfinity;
                if (!kind.Key.Equals(keep) && kind.Value.Count > 0 && at < oldestAt)
                {
                    oldest = kind.Key;
                    oldestAt = at;
                }
            }
            return !oldest.Equals(keep);
        }

        /// <summary>Destroys every waiting copy of a kind and forgets the kind.</summary>
        private static void Drop(BurstKey oldest)
        {
            if (!Idle.TryGetValue(oldest, out Stack<GameObject> idle))
            {
                return;
            }
            foreach (GameObject copy in idle)
            {
                if (copy != null)
                {
                    Object.Destroy(copy);
                }
            }
            kept -= idle.Count;
            Idle.Remove(oldest);
            LastUsed.Remove(oldest);
        }

        private static bool Restarts(GameObject prefab)
        {
            int id = prefab.GetInstanceID();
            if (!Restartable.TryGetValue(id, out bool restarts))
            {
                restarts = Array.TrueForAll(prefab.GetComponentsInChildren<Component>(true), part => RestartsAlone(part, prefab));
                Restartable[id] = restarts;
            }
            return restarts;
        }

        private static bool RestartsAlone(Component part, GameObject prefab)
        {
            if (part == null)
            {
                return false; // a missing script
            }
            if (part is ParticleSystem system && system.main.stopAction != ParticleSystemStopAction.None)
            {
                return false; // it would destroy or hide the copy itself
            }
            if (part is TimedDestruction && part.gameObject != prefab)
            {
                return false; // a part that ends before the rest
            }
            Type type = part.GetType();
            return Array.Exists(RestartableParts, allowed => allowed.IsAssignableFrom(type));
        }

        /// <summary>Removes the prefab's own timer (on its root) from a kept copy; its time, or infinity without one.</summary>
        private static float OwnTimer(GameObject clone)
        {
            float own = float.PositiveInfinity;
            foreach (TimedDestruction timer in clone.GetComponents<TimedDestruction>())
            {
                if (timer.m_triggerOnAwake)
                {
                    own = Mathf.Min(own, timer.m_timeout);
                }
                Object.Destroy(timer);
            }
            return own;
        }
    }
}

// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Adopted.BundlePrefabs;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The thrown greataxe: a copy of the skeleton archer's arrow (its ZNetView, its flight and hit handling), the
    /// arrow's model and trail taken off and the boss's axe put in, flying straight (no drop) at the attack's speed. It
    /// breaks where it hits: the game spawns the shatter (<see cref="HeadsmanShatter"/>) there, turned as the axe flew,
    /// and that is what every peer draws the pieces from. Two throws: the overhead one tumbles end over end, head over
    /// the top; the spin throw turns flat the way the boss spun (<see cref="HeadsmanFlight"/>).
    /// </summary>
    public static class HeadsmanThrow
    {
        public const string Hurled = "ECP_Headsman_axe_hurl", Disc = "ECP_Headsman_axe_disc";

        public static GameObject Build(GameObject arrow, string name, bool disc, GameObject shatter)
        {
            GameObject thrown = PrefabBench.Copy(arrow, name);
            var projectile = thrown.GetComponent<Projectile>();
            Transform visual = projectile.m_visual != null ? projectile.m_visual.transform : thrown.transform;
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            foreach (Component part in thrown.GetComponentsInChildren<Component>(true).Where(c => c is Renderer || c is ParticleSystem || c is TrailRenderer))
            {
                Object.DestroyImmediate(part);
            }
            var spin = new GameObject("spin").transform;
            spin.SetParent(visual, false);
            Transform axe = HeadsmanKit.CopyAxe(spin, "ecp_headsman_axe_thrown", HeadsmanCreature.Size);
            axe.localPosition = -(HeadsmanAxe.Spine(0.75f) * HeadsmanCreature.Size);   // turning about the haft's middle
            spin.gameObject.AddComponent<HeadsmanFlight>().Disc = disc;
            Fly(projectile, shatter);
            return thrown;
        }

        private static void Fly(Projectile projectile, GameObject shatter)
        {
            (projectile.m_gravity, projectile.m_ttl, projectile.m_rotateVisual) = (0f, 3f, 0f);
            (projectile.m_spawnOnHit, projectile.m_spawnOnHitChance, projectile.m_copyProjectileRotation) = (shatter, 1f, true);
            (projectile.m_stayAfterHitStatic, projectile.m_respawnItemOnHit, projectile.m_spawnItem) = (false, false, null);
            projectile.m_hitEffects = new EffectList();
        }
    }

    /// <summary>The thrown axe turning as it flies, on every peer: end over end (the overhead throw) or flat (the spin throw).</summary>
    public sealed class HeadsmanFlight : MonoBehaviour
    {
        /// <summary>Turns a second: 2.2 over the 0.6 s the preview's overhead throw flew, 3 over the spin throw's 0.63 s.</summary>
        private const float TumbleRate = 3.7f, DiscRate = 4.8f;

        public bool Disc;

        private void Start()
        {
            // Tumbling: the haft upright, the blade forward; flat: the haft along the flight, the blade out to the side.
            transform.localRotation = Disc ? Quaternion.Euler(90f, 0f, 90f) : Quaternion.Euler(0f, -90f, 0f);
        }

        /// <summary>About the flight's own right (head over the top) or its own down (the boss's spin), whichever way it flies.</summary>
        private void Update()
        {
            float turn = (Disc ? DiscRate : TumbleRate) * 360f * Time.deltaTime;
            transform.Rotate(Disc ? -transform.parent.up : transform.parent.right, turn, Space.World);
        }
    }
}

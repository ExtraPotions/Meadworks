// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// A skeleton the Executioner raises where its thrown axe broke, forming on every peer by the moment kept in its ZDO
    /// (<see cref="RiseKey"/>, the world's clock): nothing for <see cref="HeadsmanMoves.Hold"/>, then its bones appear
    /// one at a time from the feet up, each first in the pale ghost look (<see cref="HeadsmanGhost"/>) and then in its
    /// own colour, over the same two seconds as the Executioner's new axe; its eyes, smoke and weapon show once it
    /// stands. Its body's mesh is a copy for the forming (two submeshes: its own colour, the ghost), put back after. Its
    /// AI waits while it forms (<see cref="HeadsmanAiPatch"/>). A skeleton with no moment kept (spawned any other way)
    /// is whole from the start.
    /// </summary>
    public sealed class HeadsmanRising : MonoBehaviour
    {
        public const string RiseKey = "ecp_hs_rise";
        private const float Fade = 2.5f, Late = 0.5f;   // places a bone takes to turn; how late a sound may still play

        private ZNetView nview = null!;
        private SkinnedMeshRenderer? body;
        private HeadsmanBones? bones;
        private int[] order = new int[0];
        private Mesh? own, copy;
        private readonly List<Renderer> hidden = new List<Renderer>();
        private double start;
        private (int real, int shown) drawn = (-1, -1);
        private bool done, sounded;

        /// <summary>Whether it is still forming (or not yet known): its AI waits.</summary>
        public bool Forming => !done;

        private void Awake()
        {
            nview = GetComponent<ZNetView>();
            body = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (body != null)
            {
                body.enabled = false;   // until its first frame says whether it forms (no flash of the whole)
            }
            HeadsmanAiPatch.Watch(GetComponent<MonsterAI>(), () => Forming);
        }

        private void OnDestroy()
        {
            HeadsmanAiPatch.Forget(GetComponent<MonsterAI>());
            Restore();
        }

        private void LateUpdate()
        {
            if (done)
            {
                return;
            }
            if (start == 0d && !Begin())
            {
                return;
            }
            float since = (float)(HeadsmanTime.Now - start);
            if (since >= HeadsmanMoves.Forming)
            {
                Finish(since);
                return;
            }
            if (!sounded)
            {
                sounded = true;
                Sound("regen", since);
            }
            Reveal(since);
            Hide();
        }

        /// <summary>The moment it began to form, from its ZDO; whole at once when it has none or its body is not a skeleton's.</summary>
        private bool Begin()
        {
            if (nview.GetZDO() == null)
            {
                return false;
            }
            start = HeadsmanTime.Read(nview.GetZDO(), RiseKey);
            if (body != null)
            {
                body.enabled = true;
            }
            if (start == 0d || body == null || body.sharedMesh == null || !body.sharedMesh.isReadable)
            {
                done = true;
                return false;
            }
            Prepare(body);
            return true;
        }

        private void Prepare(SkinnedMeshRenderer skin)
        {
            own = skin.sharedMesh;
            bones = HeadsmanBones.Of(own);
            order = bones.FeetUp(skin.bones);
            copy = Instantiate(own);
            copy.subMeshCount = 2;
            skin.sharedMesh = copy;
            skin.sharedMaterials = new[] { skin.sharedMaterial, HeadsmanGhost.For(skin.sharedMaterial) };
        }

        /// <summary>How many bones are in their own colour and how many show at all, `since` it began to form.</summary>
        private float Places(float since) =>
            (since - HeadsmanMoves.Hold) / (HeadsmanMoves.Forming - 0.1f - HeadsmanMoves.Hold) * (order.Length - 1 + Fade);

        private void Reveal(float since)
        {
            float places = Places(since);
            int n = order.Length;
            (int real, int shown) now = (Mathf.Clamp(Mathf.FloorToInt(places - Fade * 0.5f) + 1, 0, n), Mathf.Clamp(Mathf.CeilToInt(places), 0, n));
            if (now == drawn || copy == null || bones == null)
            {
                return;
            }
            copy.SetTriangles(Triangles(0, now.real), 0, false);
            copy.SetTriangles(Triangles(now.real, now.shown), 1, false);
            drawn = now;
        }

        private int[] Triangles(int from, int to) =>
            order.Skip(from).Take(to - from).SelectMany(bone => bones!.Bones[bone].Triangles).ToArray();

        /// <summary>Everything but the body (eyes, smoke, weapon) hidden while it forms.</summary>
        private void Hide()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer != body && renderer.enabled)
                {
                    renderer.enabled = false;
                    hidden.Add(renderer);
                }
            }
        }

        private void Finish(float since)
        {
            Sound("solid", since - HeadsmanMoves.Forming);
            Restore();
            done = true;
        }

        /// <summary>Its own mesh and material back, and what was hidden shown.</summary>
        private void Restore()
        {
            if (body != null && own != null)
            {
                body.sharedMesh = own;
                body.sharedMaterials = new[] { body.sharedMaterials[0] };   // its own, or what another mod has put there since
            }
            if (copy != null)
            {
                Destroy(copy);
            }
            (own, copy) = (null, null);
            hidden.Where(r => r != null).ToList().ForEach(r => r.enabled = true);
            hidden.Clear();
        }

        /// <summary>The cue, `late` seconds after its moment: only when this peer sees that moment (not one who arrives later).</summary>
        private void Sound(string cue, float late)
        {
            if (late < Late)
            {
                HeadsmanSounds.Play(cue, transform.position + Vector3.up);
            }
        }

        /// <summary>Where the bone appearing `sinceFormStart` seconds into the forming is; its chest before any has.</summary>
        public Vector3 BoneAt(float sinceFormStart)
        {
            if (bones == null || order.Length == 0 || body == null)
            {
                return transform.position + Vector3.up;
            }
            int place = Mathf.Clamp(Mathf.FloorToInt(Places(sinceFormStart)), 0, order.Length - 1);
            return bones.Where(body.bones, order[place]);
        }
    }
}

// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Linq;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The Executioner's axe through a throw, on every peer, by its clip's own time: gone from the release; from the
    /// clip's Ghost a new one in the raised hands, small and ghostly for <see cref="HeadsmanMoves.Hold"/> (the same
    /// pale see-through look as the skeleton it raises, <see cref="HeadsmanGhost"/>), then growing about the right fist
    /// to full size while its pieces take their own colour one at a time, from the bottom vertebra up to the head; whole
    /// again at Solid.
    /// </summary>
    public sealed class HeadsmanForming
    {
        private const float Small = 0.3f;
        private static readonly Vector3 Fist = HeadsmanAxe.Spine(0.73f);   // the upper grip, where the right fist holds it

        private readonly Transform axe;
        private readonly Renderer[] pieces;
        private readonly Material[] own;
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;
        private bool whole = true;

        public HeadsmanForming(Transform axe)
        {
            this.axe = axe;
            pieces = axe.Cast<Transform>().Select(t => t.GetComponent<Renderer>()).Where(r => r != null).ToArray();
            own = pieces.Select(p => p.sharedMaterial).ToArray();
            (position, rotation, scale) = (axe.localPosition, axe.localRotation, axe.localScale);
        }

        public void Show(HeadsmanMove? move, float time)
        {
            bool forming = move != null && move.Forming(time);
            axe.gameObject.SetActive(move == null || move.AxeShown(time) || forming);
            if (forming)
            {
                Form(time - move!.Ghost);
            }
            else if (!whole)
            {
                Whole();
            }
        }

        private void Form(float since)
        {
            float grow = Mathf.SmoothStep(0f, 1f, (since - HeadsmanMoves.Hold) / (HeadsmanMoves.Forming - HeadsmanMoves.Hold));
            float size = Small + (1f - Small) * grow;
            axe.localScale = scale * size;
            axe.localPosition = position + rotation * Vector3.Scale(Fist, scale) * (1f - size);
            float done = (since - HeadsmanMoves.Hold) / (HeadsmanMoves.Forming - HeadsmanMoves.Hold - 0.1f) * pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].sharedMaterial = i < done ? own[i] : HeadsmanGhost.For(own[i]);
            }
            whole = false;
        }

        private void Whole()
        {
            (axe.localScale, axe.localPosition) = (scale, position);
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].sharedMaterial = own[i];
            }
            whole = true;
        }
    }
}

// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// One squirt of ink, on one machine: a straight stream of black drops from the siphon towards where its target
    /// stood, flying at <see cref="Speed"/> for about half a second of stream, stopped by the first solid thing in its
    /// way (a mast or a rail shelters you) or after <see cref="Range"/> metres, splashing where it ends. The player on
    /// this machine is hit once if the stream reaches them while they are in its line (<see cref="KrakenStrikes.Ink"/>):
    /// step out of the line, roll through it or block it facing the kraken. Nothing of it crosses the network: every
    /// machine plays its own from the same order.
    /// </summary>
    public class InkJet : MonoBehaviour
    {
        public const float Speed = 28f;
        public const float Range = 26f;
        private const float Stream = 0.45f;      // seconds from the first drop to the last
        private const float Width = 0.5f;        // the stream's reach sideways from its line
        private const float Body = 0.45f;
        private const int Drops = 9;

        private static int solids;
        private readonly List<Transform> _drops = new List<Transform>();
        private Character _kraken = null!;
        private Vector3 _from, _direction;
        private float _length, _age;
        private bool _struck;

        /// <summary>A squirt from <paramref name="from"/> towards <paramref name="target"/>, both in the world.</summary>
        public static void Launch(Character kraken, Vector3 from, Vector3 target)
        {
            var jet = new GameObject("ecp_kraken_ink").AddComponent<InkJet>();
            jet.Aim(kraken, from, target);
            KrakenEffects.Squirting(from);
        }

        private void Aim(Character kraken, Vector3 from, Vector3 target)
        {
            _kraken = kraken;
            _from = from;
            _direction = (target - from).sqrMagnitude > 1e-4f ? (target - from).normalized : kraken.transform.forward;
            solids = solids != 0 ? solids : LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "vehicle", "terrain");
            _length = Physics.Raycast(from + _direction * 0.5f, _direction, out RaycastHit hit, Range, solids, QueryTriggerInteraction.Ignore)
                ? hit.distance + 0.5f : Range;
            for (int i = 0; i < Drops; i++)
            {
                GameObject? drop = KrakenEffects.InkDrop(transform, from);
                if (drop != null)
                {
                    drop.transform.localScale *= Mathf.Lerp(1.3f, 0.6f, i / (float)Drops);
                    Array.ForEach(drop.GetComponentsInChildren<Rigidbody>(true), body => body.isKinematic = true);
                    _drops.Add(drop.transform);
                }
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float head = Mathf.Min(_age * Speed, _length);
            float tail = Mathf.Min(Mathf.Max(0f, _age - Stream) * Speed, _length);
            Place(head, tail);
            if (!_struck)
            {
                Reach(head, tail);
            }
            if (tail >= _length)
            {
                KrakenEffects.Blotting(_from + _direction * _length);
                Destroy(gameObject);
            }
        }

        // The drops strung out from the stream's head back to its tail, wobbling a little.
        private void Place(float head, float tail)
        {
            for (int i = 0; i < _drops.Count; i++)
            {
                float along = Mathf.Lerp(head, tail, i / (float)Mathf.Max(1, _drops.Count - 1));
                Vector3 wobble = new Vector3(Mathf.Sin(_age * 30f + i), Mathf.Cos(_age * 27f + i * 2f), 0f) * 0.06f;
                _drops[i].position = _from + _direction * along + wobble;
            }
        }

        // The player on this machine, if the stream between its tail and head passes through them now.
        private void Reach(float head, float tail)
        {
            Player? player = KrakenStrikes.Target();
            if (player == null || _kraken == null)
            {
                return;
            }
            Vector3 centre = player.GetCenterPoint();
            float along = Vector3.Dot(centre - _from, _direction);
            float aside = Vector3.Distance(centre, _from + _direction * along);
            if (along < tail - Body || along > head + Body || aside > Width + Body)
            {
                return;
            }
            _struck = true;
            KrakenEffects.Blotting(centre);
            KrakenStrikes.Ink(_kraken, player, _direction);
        }
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Util;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// A ground trail (Frostbound's ice, Mudbound's mud), split the way the multiplayer rule demands, as Miasmic's
    /// clouds are: the OWNER decides where patches fall - one each time the creature has walked `patch spacing` metres
    /// on the ground, at its feet, into its replicated ZDO (<see cref="GroundTrail"/>) - and EVERY machine with a
    /// player lays the same patches from it, each once, into its own <see cref="PatchLayer"/>, where they are drawn and
    /// felt and outlive the creature. It runs on every machine and gates only the drops on live ownership, so a
    /// creature handed to a new owner keeps its trail. A creature standing still, swimming, flying, falling, tamed or
    /// dead lays nothing; a jump further than any stride (a blink, a hand-over) is not walked ground and lays nothing
    /// either.
    /// </summary>
    public abstract class GroundTrailField : MonoBehaviour
    {
        /// <summary>The newest drops the blob carries: a client lays a patch as soon as it sees it, so this need only
        /// outlast the gap between two updates of the creature.</summary>
        private const int WireCap = 12;

        /// <summary>The furthest one frame's movement can be and still be walking.</summary>
        private const float MaxStride = 10f;

        private EliteController? _controller;
        private Character _character = null!;
        private TrailSpec _spec;
        private BiomeRules? _specFrom;
        private Action _step = null!;
        private Vector3 _lastPosition;
        private float _walked;
        private float _lastDropAt = float.NegativeInfinity;
        private bool _wasOwner;
        private long _seenSeq = -1L;
        private long _lastSeenId;
        private readonly List<GroundTrail.Drop> _drops = new List<GroundTrail.Drop>();

        /// <summary>Which trail this creature lays.</summary>
        private protected abstract TrailKind Kind { get; }

        protected void Start() => Guard.Run("GroundTrailField.Start", Setup);

        private void Setup()
        {
            EliteController controller = GetComponent<EliteController>();
            _character = GetComponent<Character>();
            if (controller == null || _character == null)
            {
                enabled = false;
                return;
            }
            _controller = controller;
            _lastPosition = transform.position;
            _step = Step;
        }

        protected void Update()
        {
            if (_controller != null)
            {
                Guard.Run("GroundTrailField.Update", _step);
            }
        }

        private void Step()
        {
            if (!_controller!.Ready || !_controller.View.IsValid())
            {
                return;
            }
            Refresh();
            bool owner = _controller.IsOwner();
            if (owner) // OWNER decides the trail
            {
                Walk(!_wasOwner);
            }
            _wasOwner = owner;
            if (!Headless()) // EVERY machine with a player lays and draws it
            {
                Reconstruct();
            }
        }

        // The numbers are read again whenever the creature's rules change - an edited or newly synced rule file - so a
        // retuned trail lays its next patch to the new numbers, as every other power takes them at once.
        private void Refresh()
        {
            BiomeRules rules = _controller!.Rules;
            if (!ReferenceEquals(_specFrom, rules))
            {
                _specFrom = rules;
                _spec = TrailSpec.Of(rules, _controller.Traits, Kind);
            }
        }

        /// <summary>Owner only: measure ground walked and drop a patch every `patch spacing` metres of it.</summary>
        private void Walk(bool justGained)
        {
            Vector3 here = transform.position;
            Vector3 stride = here - _lastPosition;
            stride.y = 0f;
            _lastPosition = here;
            if (justGained || stride.magnitude > MaxStride || !Lays())
            {
                _walked = 0f; // a new owner, a jump, or no ground underfoot: start measuring afresh
                return;
            }
            _walked += stride.magnitude;
            if (_walked >= _spec.Spacing && Time.time - _lastDropAt >= _spec.Gap)
            {
                _walked = 0f;
                _lastDropAt = Time.time;
                Append(here);
            }
        }

        // Tamed ones lay nothing: a patch slows every player alike, so a tame trail would turn its keepers' own base to
        // ice or mud. Nor does anything off its feet: in the air, in water, or not a living creature any more. Nor a
        // Cloning creature hiding behind its decoy: fresh ice under unseen feet would lead straight to it.
        private bool Lays() =>
            _spec.Lays && !_character.IsDead() && !_character.IsTamed() && _character.IsOnGround()
            && !_character.IsSwimming() && !_character.InWater() && !CloneStore.Hiding(_controller!.View.GetZDO());

        private void Append(Vector3 at)
        {
            ZDO zdo = _controller!.View.GetZDO();
            GroundTrail.Read(zdo, Kind.TrailKey, _drops);
            GroundTrail.Trim(_drops, _spec.Life, WireCap - 1);
            long id = GroundTrail.NextId(zdo, Kind.SeqKey);
            _drops.Add(new GroundTrail.Drop { Id = id, Pos = at, TimeMs = NetTime.NowMs() });
            GroundTrail.Write(zdo, Kind.TrailKey, _drops);
        }

        /// <summary>
        /// Every machine with a player: lay each not-yet-seen drop once, read only when the owner wrote a new one.
        /// </summary>
        private void Reconstruct()
        {
            ZDO zdo = _controller!.View.GetZDO();
            long seq = GroundTrail.Seq(zdo, Kind.SeqHash);
            if (seq == _seenSeq)
            {
                return;
            }
            _seenSeq = seq;
            GroundTrail.Read(zdo, Kind.TrailKey, _drops);
            foreach (GroundTrail.Drop drop in _drops)
            {
                if (drop.Id <= _lastSeenId)
                {
                    continue;
                }
                _lastSeenId = drop.Id;
                float remaining = Mathf.Min(_spec.Life, _spec.Life - NetTime.SecondsSince(drop.TimeMs));
                if (remaining > 0.1f)
                {
                    PatchLayer.For(Kind).Lay(drop.Id, drop.Pos, _spec, remaining);
                }
            }
        }

        private static bool Headless() => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}

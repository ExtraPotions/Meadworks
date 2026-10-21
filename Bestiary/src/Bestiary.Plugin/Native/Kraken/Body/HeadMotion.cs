// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// The head on one machine: where it is and the attack it is making. Swimming, it follows the creature: at the
    /// surface while it hunts (leaning back so the mantle breaks the water behind its eyes), deeper while it lurks,
    /// sinking away when it leaves. Holding a ship it is placed in the ship's own space: upside down under the hull in
    /// the tentacle phase (head deep, body and tentacle roots reaching up to just under the keel; it rolls over as it
    /// dives), up beside the ship facing the deck in the head phase, where its bites and ink move it on top while
    /// its body's lower end stays in the water where it was, so the body bends back over the rail instead of going through
    /// the hull. It glides between places: sideways quickly, up and down slowly, so it rises beside the hull.
    /// </summary>
    public class HeadMotion
    {
        public enum Place { Hunt, Lurk, Sink, Under, Beside }

        private const float HuntDepth = 2f, LurkDepth = 5.5f, SinkRate = 2.5f, SwimPitch = -35f;
        private const float UnderKeel = 1.5f;    // metres under the waterline the upturned body's end reaches
        private const float MouthAhead = 1.9f;   // metres from the head's base to the beak's tip at the end of a lunge, and some

        private static readonly Quaternion Upturned = Quaternion.Euler(180f, 0f, 0f);   // head down, body up at the hull

        private readonly HeadRig _rig;
        private Transform? _space;          // the ship whose space _at and _turn are in; null: the world's
        private Vector3 _at;
        private Quaternion _turn = Quaternion.identity;
        private bool _placed;
        private float _scale = 1f;

        public HeadMotion(HeadRig rig) => _rig = rig;

        public Place Where { get; set; } = Place.Lurk;
        public HeadAction? Action { get; private set; }

        /// <summary>
        /// An attack begins. A bite lunges as far as its target needs: the usual lunge over the rail for someone at it,
        /// the whole head thrown onto the deck for someone further in.
        /// </summary>
        public void Begin(HeadAction action)
        {
            if (action.What == HeadAction.Kind.Bite && _space != null)
            {
                Vector3 towards = action.Target - _at;
                towards.y = 0f;
                action.Lunge = Mathf.Clamp(towards.magnitude / _scale - MouthAhead, HeadAction.MinLunge, HeadAction.MaxLunge);
            }
            Action = action;
        }

        public void Flinch() => Action?.Flinch();

        /// <summary>Away from any ship, placed from the creature's position and heading; <paramref name="age"/> since it began sinking.</summary>
        public void Swimming(Transform root, float scale, float age, float time, float dt)
        {
            IntoWorld();
            Action = null;
            Vector3 at = root.position;
            float depth = Where == Place.Hunt ? HuntDepth : Where == Place.Lurk ? LurkDepth : LurkDepth + SinkRate * age;
            Vector3 target = new Vector3(at.x, KrakenShips.Water(at) - depth * scale, at.z);
            Vector3 heading = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            Quaternion turn = Quaternion.LookRotation(heading.sqrMagnitude > 1e-4f ? heading : Vector3.forward) * Quaternion.Euler(SwimPitch, 0f, 0f);
            Follow(target, turn, dt, Where == Place.Hunt ? 8f : 3f, 3f);
            Apply(_at, _turn, default, time, null);
        }

        /// <summary>At the ship. Returns the attack whose moment comes this frame, if any (marked dealt), for the caller to deal.</summary>
        public HeadAction? AtShip(ShipScene scene, int side, float along, float scale, float time, float dt)
        {
            IntoShip(scene.Transform);
            _scale = Mathf.Max(scale, 0.01f);
            ShipHull hull = scene.Hull;
            bool beside = Where == Place.Beside;
            Vector3 target = beside
                ? new Vector3(side * (hull.HalfWidth + KrakenTargets.BesideGap * scale), scene.Waterline - KrakenTargets.BesideDepth * scale, along)
                : new Vector3(0f, scene.Waterline - (UnderKeel + _rig.BodyLength) * scale, hull.MiddleZ);
            Quaternion rest = beside ? Quaternion.LookRotation(new Vector3(-side, 0f, 0f)) : Upturned;
            Follow(target, rest, dt, 2.5f, 1.3f);
            HeadOffsets offsets = beside && Action != null ? Action.Offsets() : default;
            Quaternion face = Facing(offsets.Turn);
            Vector3 at = _at + Vector3.up * (offsets.Raise + (beside ? 0.12f * Mathf.Sin(time * 0.9f) : 0f)) * scale
                + face * Vector3.forward * offsets.Forward * scale;
            Vector3? body = beside ? scene.World(_at - Vector3.up * _rig.BodyLength * scale) : (Vector3?)null;
            Apply(scene.World(at), scene.Transform.rotation * face, offsets, time, body);
            return Fire(beside);
        }

        private HeadAction? Fire(bool beside)
        {
            HeadAction? action = Action;
            if (action == null || !beside)
            {
                Action = null;
                return null;
            }
            Action = action.Done ? null : action;
            if (!action.Firing)
            {
                return null;
            }
            action.Fired = true;
            return action;
        }

        // Turned from facing the ship towards the attack's target, by `turn`.
        private Quaternion Facing(float turn)
        {
            if (Action == null || turn <= 0f)
            {
                return _turn;
            }
            Vector3 towards = Action.Target - _at;
            towards.y = 0f;
            return towards.sqrMagnitude < 1e-4f ? _turn : Quaternion.Slerp(_turn, Quaternion.LookRotation(towards), turn);
        }

        // `body`: where the body's lower end stays (the head beside a ship), so an attack bends it rather than moving it.
        private void Apply(Vector3 position, Quaternion rotation, HeadOffsets offsets, float time, Vector3? body)
        {
            _rig.Apply(new HeadPose
            {
                Position = position, Rotation = rotation, Time = time, Life = 1f, Siphon = offsets.Siphon,
                Lean = offsets.Lean + 4f * Mathf.Sin(time * 0.8f),
                Beak = Mathf.Max(offsets.Beak, 0.08f + 0.08f * Mathf.Sin(time * 1.3f)),
                BodyEnd = body,
            });
        }

        private void Follow(Vector3 target, Quaternion turn, float dt, float level, float vertical)
        {
            if (!_placed)
            {
                (_at, _turn, _placed) = (target, turn, true);
                return;
            }
            float h = Motion.Ease.Follow(level, dt);
            _at = new Vector3(Mathf.Lerp(_at.x, target.x, h), Mathf.Lerp(_at.y, target.y, Motion.Ease.Follow(vertical, dt)), Mathf.Lerp(_at.z, target.z, h));
            _turn = Quaternion.Slerp(_turn, turn, Motion.Ease.Follow(vertical, dt));
        }

        private void IntoShip(Transform ship)
        {
            if (_space == ship)
            {
                return;
            }
            IntoWorld();
            _at = ship.InverseTransformPoint(_at);
            _turn = Quaternion.Inverse(ship.rotation) * _turn;
            _space = ship;
        }

        // Back into the world's space; if the ship it was placed on is gone, it starts afresh where it is next put.
        private void IntoWorld()
        {
            if (ReferenceEquals(_space, null))
            {
                return;
            }
            if (_space != null)
            {
                _at = _space.TransformPoint(_at);
                _turn = _space.rotation * _turn;
            }
            else
            {
                _placed = false;
            }
            _space = null;
        }
    }
}

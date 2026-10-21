// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Bestiary.Runtime.Native.Kraken.Motion;
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Kraken
{
    /// <summary>
    /// One long tentacle on one machine: what it is doing (trailing behind the swimming head, up out of the water beside
    /// the ship, gripping the rail, or down under the water), how far out of the water it has risen, and the blow it is
    /// dealing, if any. Each frame it works out its pose, eases the shown pose towards it (a blow is followed exactly, so
    /// its timing is crisp) and puts the bones on it. Fully under the water it is not drawn at all.
    /// </summary>
    public class TentacleMotion
    {
        public enum Mode { Trail, Up, Grip, Down }

        private const float Rise = 1f / 1.3f;     // share of the way out of the water per second
        private const float Sink = 1f / 1.0f;
        private const float Settle = 7f;         // per second, easing towards a new pose
        private const float GripReach = 1.3f;    // metres of deck a gripping tentacle lies on

        private readonly TentacleRig _rig;
        private readonly Renderer[] _renderers;
        private readonly Vector3[] _shown = TentacleChain.New(), _rest = TentacleChain.New();
        private readonly Vector3[] _a = TentacleChain.New(), _b = TentacleChain.New();
        private DeckProfile? _grip;
        private float? _gripAlong;
        private Vector3 _side = Vector3.right;
        private float _emerged;
        private bool _placed, _drawn = true;

        public TentacleMotion(int index, TentacleRig rig)
        {
            Index = index;
            _rig = rig;
            _renderers = rig.Root.GetComponentsInChildren<Renderer>(true);
        }

        public int Index { get; }
        public Mode Current { get; private set; } = Mode.Trail;
        public TentacleStrike? Strike { get; private set; }

        /// <summary>The pose it is drawn in now.</summary>
        public Vector3[] Shown => _shown;

        /// <summary>
        /// What it does now. A gripping tentacle rises at <paramref name="gripAlong"/> along the hull (beside the head)
        /// rather than at its own place.
        /// </summary>
        public void Set(Mode mode, float? gripAlong = null)
        {
            if (mode == Mode.Grip && (mode != Current || gripAlong != _gripAlong))
            {
                _grip = null;
            }
            _gripAlong = mode == Mode.Grip ? gripAlong : null;
            if (Current == Mode.Trail && mode != Mode.Trail)
            {
                _emerged = 0f;    // it rises round the ship from under the water, not from where it trailed
            }
            Current = mode;
        }

        public void Begin(TentacleStrike strike) => Strike = strike;

        public void Flinch() => Strike?.Flinch(_shown);

        /// <summary>
        /// Beside the ship. Returns the blow that lands this frame, if any (marked dealt), for the caller to deal.
        /// </summary>
        public TentacleStrike? AtShip(ShipScene scene, float scale, float time, float dt)
        {
            float target = Current == Mode.Up || Current == Mode.Grip ? 1f : 0f;
            _emerged = Mathf.MoveTowards(_emerged, target, (target > _emerged ? Rise : Sink) * dt);
            TentacleFrame across = scene.Across(Index, scale, _gripAlong);
            RestPose(scene, across, scale, time);
            TentacleStrike? landing = Strike != null && Strike.Landing ? Strike : null;
            if (Strike != null)
            {
                Strike.Pose(scene, scale, time, _rest, _shown, _a, _b);
                Show(Strike.Frame(scene, scale).Side, dt);
                Strike = Strike.Done ? null : Strike;
            }
            else
            {
                EaseTowards(_rest, dt, Settle);
                Show(across.Side, dt);
            }
            if (landing != null)
            {
                landing.Struck = true;
            }
            return landing;
        }

        /// <summary>Behind the swimming head, from its joint (the frame's Out points the way it trails).</summary>
        public void Trailing(TentacleFrame frame, float time, float dt)
        {
            Strike = null;
            _emerged = 1f;
            TentacleSwim.Trail(frame, time, Index * 1.7f, _rest);
            EaseTowards(_rest, dt, Settle * 2f);
            Show(frame.Side, dt);
        }

        /// <summary>Put straight onto a pose (a dead kraken's limbs).</summary>
        public void Hold(Vector3[] pose, Vector3 side)
        {
            TentacleChain.Copy(pose, _shown);
            _placed = true;
            Draw(true);
            _rig.Pose(_shown, side);
        }

        private void RestPose(ShipScene scene, TentacleFrame across, float scale, float time)
        {
            if (Current == Mode.Grip)
            {
                _grip ??= scene.Deck(Index, new Vector3(0f, 0f, scene.Anchor(Index, _gripAlong).z), _gripAlong);
                TentacleDeck.Build(across, _grip.Value, GripReach, _rest);
                TentacleCurl.Emerge(across, _emerged, _rest);
            }
            else
            {
                TentacleCurl.Rising(across, TentacleCurl.Idle.For(Index), _emerged, time, Index, _rest);
            }
        }

        private void EaseTowards(Vector3[] pose, float dt, float rate)
        {
            if (!_placed)
            {
                TentacleChain.Copy(pose, _shown);
                _placed = true;
                return;
            }
            TentacleChain.Lerp(_shown, pose, Motion.Ease.Follow(rate, dt), _shown);
            TentacleChain.Straighten(_shown, TentacleSpec.Segment * _rig.Root.lossyScale.x);
        }

        private void Show(Vector3 side, float dt)
        {
            _side = Vector3.Slerp(_side, side, Motion.Ease.Follow(Settle, dt));
            bool drawn = Strike != null || Current == Mode.Trail || _emerged > 0.001f;
            Draw(drawn);
            if (drawn)
            {
                _rig.Pose(_shown, _side);
            }
        }

        private void Draw(bool drawn)
        {
            if (drawn == _drawn)
            {
                return;
            }
            _drawn = drawn;
            foreach (Renderer renderer in _renderers)
            {
                renderer.enabled = drawn;
            }
        }
    }
}
